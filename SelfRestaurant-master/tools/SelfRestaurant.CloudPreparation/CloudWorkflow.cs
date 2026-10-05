using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SelfRestaurant.Billing.Api.Infrastructure;
using SelfRestaurant.Billing.Api.Infrastructure.Payments;
using SelfRestaurant.Billing.Api.Persistence;
using SelfRestaurant.Billing.Api.Persistence.Entities;

internal static class CloudWorkflow
{
    internal static string Stage { get; private set; } = "not started";
    public static string RuntimeConnection(Input input, string owner) => new NpgsqlConnectionStringBuilder
    {
        Host = input.Host, Port = 6543, Database = "postgres", Username = "viettable_" + owner + "." + input.ProjectRef,
        Password = input.RolePasswords[owner], SslMode = SslMode.VerifyFull, RootCertificate = "/staging-ca/supabase-ca.crt",
        Pooling = input.RuntimePooling, NoResetOnClose = input.RuntimeNoReset, Multiplexing = false,
        MaxPoolSize = 2, MinPoolSize = 0, MaxAutoPrepare = 0, IncludeErrorDetail = false, Timeout = 15,
        CommandTimeout = 30, ApplicationName = "VietTable-Staging-Acceptance-" + owner
    }.ConnectionString;

    public static async Task RunAsync(Input input, Func<string, DbContext> context)
    {
        await PoolerRoundTripAsync(context);
        var processes = new List<Process>();
        var signing = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        try
        {
            foreach (var (owner, port) in new[] { ("Gateway",6100),("Catalog",6101),("Orders",6102),("Customers",6103),("Identity",6104),("Billing",6105) })
            {
                var directory = Path.Combine("/workspace/src", owner == "Gateway" ? "Gateway" : "Services", "SelfRestaurant." + owner + ".Api");
                var start = new ProcessStartInfo("dotnet", "bin/Debug/net8.0/SelfRestaurant." + owner + ".Api.dll")
                    { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                start.Environment["PORT"] = port.ToString(); start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
                start.Environment["Logging__LogLevel__Default"] = "None";
                start.Environment["Database__Provider"] = "Postgres"; start.Environment["Database__SeedProfile"] = "None";
                start.Environment["Database__ApplyMigrations"] = "false"; start.Environment["MobileJwt__SigningKey"] = signing;
                start.Environment["RabbitMq__Enabled"] = "false"; start.Environment["IntegrationEvents__WriteDiagnosticFiles"] = "false";
                start.Environment["TableAutoRelease__Enabled"] = "false"; // Bounded test only, not a cloud deployment setting.
                foreach (var (service, servicePort) in new[] { ("Catalog",6101),("Orders",6102),("Customers",6103),("Identity",6104),("Billing",6105) })
                    start.Environment["Services__" + service] = "http://127.0.0.1:" + servicePort;
                if (owner != "Gateway") start.Environment["ConnectionStrings__" + owner + "Db"] = RuntimeConnection(input, owner.ToLowerInvariant());
                var process = Process.Start(start) ?? throw new InvalidOperationException("Staging API process failed to start.");
                process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine(); process.BeginErrorReadLine(); processes.Add(process);
            }
            using var gateway = Client(6100); using var catalog = Client(6101); using var orders = Client(6102);
            foreach (var port in Enumerable.Range(6100, 6))
            {
                using var health = Client(port);
                var ready = false;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    if (processes.Any(p => p.HasExited)) throw new InvalidOperationException("Staging API exited during readiness.");
                    try { using var response = await health.GetAsync("/readyz"); ready = response.IsSuccessStatusCode; }
                    catch (HttpRequestException) { }
                    if (ready) break;
                    await Task.Delay(500);
                }
                Ensure(ready, "Staging API readiness timeout.");
            }
            Console.WriteLine("PASS: six isolated local APIs ready; five owner DB logins via transaction pooler; Gateway no DB credential.");
            var suffix = Guid.NewGuid().ToString("N"); var username = "cloudqa_" + suffix;
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "!a1";
            var registration = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/register", new {
                name = "Staging acceptance", username, password, email = username + "@example.test",
                phoneNumber = "09" + RandomNumberGenerator.GetInt32(10000000,99999999) });
            Console.WriteLine("Retained staging acceptance customer: ID=" + registration.GetProperty("user").GetProperty("customerId").GetInt32());
            var login = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/login", new { username = username.ToUpperInvariant(), password });
            var refresh = await Send(gateway, HttpMethod.Post, "/api/mobile/v1/auth/refresh", new { refreshToken = login.GetProperty("refreshToken").GetString() });
            gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refresh.GetProperty("accessToken").GetString());
            _ = await Send(gateway,HttpMethod.Get,"/api/mobile/v1/me");
            Console.WriteLine("PASS: Identity register/login/refresh + authenticated customer profile.");
            var branches = await Send(gateway,HttpMethod.Get,"/api/mobile/v1/branches"); var branchId = branches[0].GetProperty("branchId").GetInt32();
            var layout = await Send(catalog,HttpMethod.Get,$"/api/branches/{branchId}/tables");
            var table = layout.GetProperty("tables").EnumerateArray().First(x => x.GetProperty("isAvailable").GetBoolean());
            var tableId = table.GetProperty("tableId").GetInt32();
            var menu = await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/branches/{branchId}/menu");
            var dish = menu.GetProperty("categories").EnumerateArray().SelectMany(c => c.GetProperty("dishes").EnumerateArray())
                .First(x => x.GetProperty("available").GetBoolean());
            Console.WriteLine("PASS: Catalog branch/table/menu API reads from Supabase canonical bootstrap.");
            var reservationBody = new { branchId, tableId, partySize=2, reservedAt=DateTimeOffset.UtcNow.AddDays(7), note="Staging concurrency acceptance" };
            var reservations = await Race(gateway,"/api/mobile/v1/reservations",reservationBody,suffix+"-reservation",sameKey:false);
            var reservationId = reservations.GetProperty("reservationId").GetInt32();
            _ = await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/reservations/{reservationId}");
            _ = await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/reservations/{reservationId}/cancel", new {},suffix+"-cancel");
            _ = await Send(gateway,HttpMethod.Get,"/api/mobile/v1/reservations?scope=history");
            Console.WriteLine($"PASS: reservation allocation concurrency/no500, detail/history; reservation {reservationId} cancelled via API.");
            var session = await Race(gateway,"/api/mobile/v1/check-ins/walk-in",new { qrCode=table.GetProperty("qrCode").GetString() },suffix+"-walkin");
            var sessionId = session.GetProperty("diningSessionId").GetInt32();
            Console.WriteLine($"Retained staging session: {sessionId}; branch={branchId}; table={tableId}.");
            var orderBody = new { items=new[] {new {dishId=dish.GetProperty("dishId").GetInt32(),quantity=1}} };
            var orderPath=$"/api/mobile/v1/dining-sessions/{sessionId}/orders";
            var first=await Race(gateway,orderPath,orderBody,suffix+"-round1");
            var replay=await Send(gateway,HttpMethod.Post,orderPath,orderBody,suffix+"-round1");
            var orderId=first.GetProperty("orderId").GetInt32(); Ensure(orderId==replay.GetProperty("orderId").GetInt32(),"Order replay mismatch.");
            var second=await Send(gateway,HttpMethod.Post,orderPath,orderBody,suffix+"-round2");
            Ensure(orderId!=second.GetProperty("orderId").GetInt32(),"Round2 overwrote first order.");
            await Send(orders,HttpMethod.Post,$"/api/orders/{orderId}/chef/start");
            await Send(orders,HttpMethod.Post,$"/api/orders/{orderId}/chef/ready");
            var detail=await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/orders/{orderId}");
            var item=detail.GetProperty("items")[0].GetProperty("itemId").GetInt32();
            await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/orders/{orderId}/items/{item}/confirm-received");
            var serviceRequest=await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/service-requests",new {requestType="WATER"},suffix+"-water");
            var cancelled=await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/service-requests/{serviceRequest.GetProperty("serviceRequestId").GetInt32()}/cancel");
            Ensure(cancelled.GetProperty("status").GetString()=="CANCELLED","Service cancel mismatch.");
            Console.WriteLine("PASS: check-in/order submit concurrency, two rounds, idempotency, kitchen/received transitions and service request cancel.");
            var quote=await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/checkout/quote",new {},suffix+"-quote");
            var quoteId=quote.GetProperty("checkoutQuoteId").GetInt32();
            Ensure(quote.GetProperty("totalAmount").GetDecimal()==dish.GetProperty("price").GetDecimal()*2,"Quote money mismatch.");
            await Send(gateway,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/payment-request",new {checkoutQuoteId=quoteId});
            var paymentBody=new {channel="ONSITE",method="CASH",location="TABLE"};
            var paymentPath=$"/api/mobile/v1/checkout/quotes/{quoteId}/payments";
            var payment=await Send(gateway,HttpMethod.Post,paymentPath,paymentBody,suffix+"-payment");
            var paymentId=payment.GetProperty("paymentId").GetInt32();
            var retry=await Send(gateway,HttpMethod.Post,paymentPath,paymentBody,suffix+"-payment");
            Ensure(paymentId==retry.GetProperty("paymentId").GetInt32(),"Payment replay mismatch.");
            await using var billing=(BillingDbContext)context("billing"); await using var competitor=(BillingDbContext)context("billing");
            using var identity=Client(6104);
            var finalizer=new PaymentFinalizationService(billing,new OrdersApiClient(orders,billing),new CustomersApiClient(identity),NullLogger<PaymentFinalizationService>.Instance);
            var competing=new PaymentFinalizationService(competitor,new OrdersApiClient(orders,competitor),new CustomersApiClient(identity),NullLogger<PaymentFinalizationService>.Instance);
            await Task.WhenAll(finalizer.ConfirmAsync(paymentId,null,null,null,CancellationToken.None),competing.ConfirmAsync(paymentId,null,null,null,CancellationToken.None));
            var paid=await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/payments/{paymentId}");
            Ensure(paid.GetProperty("status").GetString()=="SUCCEEDED","Payment not succeeded.");
            var invoice=await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}/invoice");
            Ensure(invoice.GetProperty("totalAmount").GetDecimal()==quote.GetProperty("totalAmount").GetDecimal(),"Invoice money mismatch.");
            var closed=await Send(gateway,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}");
            Ensure(closed.GetProperty("status").GetString()=="CLOSED","Session not closed.");
            Ensure(await billing.Invoices.CountAsync(x=>x.DiningSessionId==sessionId)==1,"Duplicate final invoice.");
            var tables=await Send(catalog,HttpMethod.Get,$"/api/branches/{branchId}/tables");
            Ensure(tables.GetProperty("tables").EnumerateArray().Single(x=>x.GetProperty("tableId").GetInt32()==tableId).GetProperty("isAvailable").GetBoolean(),"Table not released.");
            Console.WriteLine($"PASS: concurrent Billing domain finalization, ONSITE SUCCEEDED/invoice exact total/session CLOSED/table released; paymentId={paymentId}; sessionId={sessionId}.");
            Console.WriteLine("INFO: financial/history QA rows retained intentionally; no SQL DELETE. This does not claim Cashier UI/auth acceptance.");
        }
        finally
        {
            foreach(var process in processes)
            { if(!process.HasExited) process.Kill(entireProcessTree:true); await process.WaitForExitAsync(); process.Dispose(); }
        }
    }

    private static HttpClient Client(int port)=>new(){BaseAddress=new Uri("http://127.0.0.1:"+port),Timeout=TimeSpan.FromSeconds(90)};
    private static async Task PoolerRoundTripAsync(Func<string,DbContext> context)
    {
        Stage="pooler EF round trip";
        await using(var db=(BillingDbContext)context("billing"))
        {
            await using var tx=await db.Database.BeginTransactionAsync();
            var marker="CLOUD-ROLLBACK-"+Guid.NewGuid().ToString("N"); var now=DateTime.UtcNow;
            db.CheckoutQuotes.Add(new CheckoutQuote {QuoteCode=marker,IdempotencyKey=marker,RequestHash=marker,DiningSessionCode=marker,
                ItemsJson="[]",Status="OPEN",Subtotal=35000.25m,TotalAmount=35000.25m,CreatedAtUtc=now,UpdatedAtUtc=now,ExpiresAtUtc=now.AddMinutes(15)});
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var read=await db.CheckoutQuotes.SingleAsync(x=>x.IdempotencyKey==marker);
            Ensure(read.TotalAmount==35000.25m&&read.CreatedAtUtc.Kind==DateTimeKind.Utc
                &&Math.Abs((read.CreatedAtUtc-now).TotalMilliseconds)<1,"Pooler money/UTC round trip mismatch.");
            await tx.RollbackAsync();
        }
        await using(var first=(BillingDbContext)context("billing"))
        await using(var second=(BillingDbContext)context("billing"))
        {
            Stage="pooler first transaction begin";
            await using var tx1=await first.Database.BeginTransactionAsync();
            var lockId=RandomNumberGenerator.GetInt32(1000000000,int.MaxValue);
            Stage="pooler first SET LOCAL";
            await first.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'");
            Stage="pooler first advisory lock";
            await PaymentFinalizationService.LockQuoteAsync(first,lockId,CancellationToken.None);
            Stage="pooler competing advisory lock release";
            async Task Compete()
            {
                await using var tx2=await second.Database.BeginTransactionAsync();
                await PaymentFinalizationService.LockQuoteAsync(second,lockId,CancellationToken.None);
                await tx2.CommitAsync();
            }
            var waiting=Compete();
            Ensure(!waiting.IsCompleted,"Competing advisory lock did not wait.");
            await tx1.CommitAsync(); await waiting.WaitAsync(TimeSpan.FromSeconds(30));
        }
        for(var cycle=0;cycle<3;cycle++)
        {
            Stage="pooler connection reuse";
            await using var db=context("billing"); _=await ((BillingDbContext)db).CheckoutQuotes.CountAsync();
        }
        Console.WriteLine("PASS: actual transaction pooler EF SaveChanges/rollback/exact numeric/UTC/advisory-lock contention and connection reuse; AutoPrepare0.");
    }
    private static void Ensure(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
    private static async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? key=null)
    {
        using var request=new HttpRequestMessage(method,path);
        if(body is not null)request.Content=JsonContent.Create(body);
        if(key is not null)request.Headers.Add("Idempotency-Key",key);
        request.Headers.Add("X-Correlation-Id","staging-"+Guid.NewGuid().ToString("N"));
        using var response=await client.SendAsync(request);
        if(!response.IsSuccessStatusCode)throw new CloudAcceptanceException($"{method} {path}: HTTP {(int)response.StatusCode}");
        var text=await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text)?"{}":text).RootElement.Clone();
    }
    private static async Task<JsonElement> Race(HttpClient client,string path,object body,string key,bool sameKey=true)
    {
        async Task<HttpResponseMessage> Submit(string id){using var request=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};request.Headers.Add("Idempotency-Key",id);return await client.SendAsync(request);}
        var responses=await Task.WhenAll(Submit(key),Submit(sameKey?key:key+"-other"));
        try
        {
            Ensure(responses.Any(r=>r.IsSuccessStatusCode)&&responses.All(r=>r.IsSuccessStatusCode||r.StatusCode==HttpStatusCode.Conflict),"Concurrency unexpected status/no500.");
            if(!sameKey)Ensure(responses.Count(r=>r.IsSuccessStatusCode)==1,"Two conflicting reservations succeeded.");
            return JsonDocument.Parse(await responses.First(r=>r.IsSuccessStatusCode).Content.ReadAsStringAsync()).RootElement.Clone();
        }
        finally{foreach(var response in responses)response.Dispose();}
    }
}

internal sealed class CloudAcceptanceException(string safeMessage) : Exception(safeMessage);
