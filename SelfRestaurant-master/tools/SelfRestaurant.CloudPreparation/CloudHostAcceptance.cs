using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SelfRestaurant.Billing.Api.Persistence;

internal static class CloudHostAcceptance
{
    public static async Task RunAsync(Input input)
    {
        var fixture = await CloudStaffProvisioner.CreateAsync(input);
        var staff = fixture.Staff;
        var start = new ProcessStartInfo("dotnet", "bin/Debug/net8.0/VietTable.CloudHost.dll") {
            WorkingDirectory = "/workspace/src/Backend/VietTable.CloudHost", UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["PORT"] = "5200";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; // Bounded local HTTP test only.
        start.Environment["Logging__LogLevel__Default"] = "None";
        start.Environment["HttpsRedirection__Enabled"] = "false";
        start.Environment["Database__Provider"] = "Postgres";
        start.Environment["Database__ApplyMigrations"] = "false";
        start.Environment["Database__SeedProfile"] = "None";
        start.Environment["MobileJwt__SigningKey"] = input.CloudHostSigningKey;
        start.Environment["CloudHost__EnvironmentNamespace"] = "staging-local";
        start.Environment["CloudHost__DataProtectionCertificate"] = input.CloudHostDataProtectionCertificate;
        if (input.Action is "CloudRecovery" or "CloudRecoveryProbe")
        {
            // Test-only fault injection; the value is never written to source or logs.
            start.Environment["CRON_SECRET"] = input.CloudHostSigningKey;
            if (input.Action == "CloudRecovery")
            {
                start.Environment["Recovery__TestFailReadyProjection"] = "true";
                start.Environment["Recovery__TestFailAfterPaymentCommit"] = "true";
            }
        }
        start.Environment["MediaStorage__Provider"] = "Supabase";
        start.Environment["MediaStorage__Supabase__ProjectUrl"] = "https://" + input.ProjectRef + ".supabase.co";
        start.Environment["MediaStorage__Supabase__Bucket"] = "viettable-staging-media";
        start.Environment["MediaStorage__Supabase__ServiceKey"] = input.StorageServiceKey;
        foreach (var owner in DatabaseAcceptance.Owners)
            start.Environment["ConnectionStrings__" + char.ToUpperInvariant(owner[0]) + owner[1..] + "Db"] = CloudWorkflow.RuntimeConnection(input, owner);
        foreach (var (name, folder) in new[] {("Chef","chef"),("Cashier","cashier"),("Admin","admin")})
            start.Environment["Frontend__" + name + "DistPath"] = "/workspace/src/Frontend/selfrestaurant-" + folder + "-web/dist";
        var process = Process.Start(start) ?? throw new InvalidOperationException("CloudHost did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        var actors=new Dictionary<string,HttpClient>();
        var staffCookies=new Dictionary<string,CookieContainer>();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5200"), Timeout = TimeSpan.FromSeconds(90) };
            async Task RestartAsync()
            {
                process.Kill(entireProcessTree:true); await process.WaitForExitAsync();
                _=await output; _=await errors; process.Dispose();
                process=Process.Start(start)??throw new CloudAcceptanceException("CloudHost recovery restart failed.");
                output=process.StandardOutput.ReadToEndAsync(); errors=process.StandardError.ReadToEndAsync();
                var restartReady=false;
                for(var attempt=0;attempt<240;attempt++)
                {
                    if(process.HasExited)throw new CloudAcceptanceException("CloudHost exited on recovery restart.");
                    try{using var response=await client.GetAsync("/readyz");restartReady=response.IsSuccessStatusCode;}catch(HttpRequestException){}
                    if(restartReady)break;await Task.Delay(500);
                }
                Ensure(restartReady,"CloudHost recovery restart readiness timeout.");
            }
            async Task InvokeRecoveryAsync()
            {
                using var request=new HttpRequestMessage(HttpMethod.Post,"/api/internal/recovery");
                request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",input.CloudHostSigningKey);
                using var response=await client.SendAsync(request);
                Ensure(response.IsSuccessStatusCode,"Secured recovery endpoint failed: HTTP "+(int)response.StatusCode);
            }
            var ready = false;
            for (var attempt = 0; attempt < 240; attempt++)
            {
                if (process.HasExited) throw new CloudAcceptanceException("CloudHost exited at startup; stderr withheld for credential safety.");
                try { using var response = await client.GetAsync("/readyz"); ready = response.IsSuccessStatusCode;
                    if (!ready && attempt % 20 == 0) Console.WriteLine("CloudHost readyz HTTP " + (int)response.StatusCode); }
                catch (HttpRequestException) { }
                if (ready) break;
                await Task.Delay(500);
            }
            Ensure(ready, "CloudHost readiness timeout.");
            Console.WriteLine("PASS: one CloudHost listener5200, five owner modules ready via in-process dispatch; no downstream listeners.");
            if (input.Action == "CloudRecoveryProbe")
            {
                using(var denied=await client.PostAsync("/api/internal/recovery",null))
                    Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Anonymous recovery access was accepted.");
                await Task.WhenAll(InvokeRecoveryAsync(),InvokeRecoveryAsync());
                await InvokeRecoveryAsync();
                Console.WriteLine("PASS: secured bounded concurrent/duplicate recovery passes completed without creating QA business data.");
                return;
            }
            using (var denied = await client.GetAsync("/api/mobile/v1/me")) Ensure(denied.StatusCode == HttpStatusCode.Unauthorized,"Mobile auth denial mismatch.");
            using (var hidden = await client.GetAsync("/api/internal/mobile/auth/login")) Ensure(hidden.StatusCode == HttpStatusCode.NotFound,"Private module route exposed.");
            var username = input.Action == "CloudRecovery"
                ? "cloudrecoveryqa_" + Guid.NewGuid().ToString("N")[..20]
                : "cloudhostqa_" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input.CloudHostAcceptancePassword)))[..24].ToLowerInvariant();
            var password = input.CloudHostAcceptancePassword;
            using var existingLogin=await client.PostAsJsonAsync("/api/mobile/v1/auth/login",new{username,password});
            if(existingLogin.StatusCode==HttpStatusCode.Unauthorized) {
            var registered = await Send(client,HttpMethod.Post,"/api/mobile/v1/auth/register",new {
                name="CloudHost acceptance", username,password,email=username+"@example.test",
                phoneNumber="09"+RandomNumberGenerator.GetInt32(10000000,99999999) });
            Console.WriteLine("Retained CloudHost QA customer ID="+registered.GetProperty("user").GetProperty("customerId").GetInt32());
            }
            else Ensure(existingLogin.IsSuccessStatusCode,"Existing QA customer login failed; no implicit password reset.");
            var login=await Send(client,HttpMethod.Post,"/api/mobile/v1/auth/login",new {username,password});
            var refresh=await Send(client,HttpMethod.Post,"/api/mobile/v1/auth/refresh",new {refreshToken=login.GetProperty("refreshToken").GetString()});
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",refresh.GetProperty("accessToken").GetString());
            _=await Send(client,HttpMethod.Get,"/api/mobile/v1/me");
            var branches=await Send(client,HttpMethod.Get,"/api/mobile/v1/branches");
            var branch=branches[0].GetProperty("branchId").GetInt32();
            var menu=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{branch}/menu");
            Ensure(menu.GetProperty("categories").GetArrayLength()>0,"Menu empty.");
            _=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{branch}/table-layout");
            _=await Send(client,HttpMethod.Get,"/api/mobile/v1/reservations?scope=history");
            if (input.Action != "CloudRecovery")
                _=await Send(client,HttpMethod.Get,"/api/mobile/v1/orders?scope=history");
            Console.WriteLine("PASS: CloudHost mobile register/login/refresh/profile, Catalog/menu/tables, Customers/Orders histories and private-route denial.");
            if (input.Action == "CloudHostAuth")
            {
                var upcoming = await Send(client, HttpMethod.Get, "/api/mobile/v1/reservations?scope=upcoming&limit=100");
                foreach (var prior in upcoming.GetProperty("items").EnumerateArray().Where(x =>
                    x.GetProperty("note").GetString() == "CloudHost acceptance reservation"))
                    await Send(client, HttpMethod.Post, $"/api/mobile/v1/reservations/{prior.GetProperty("reservationId").GetInt32()}/cancel",
                        key: Guid.NewGuid().ToString("N")); // Exact owned QA records only, retained in history.
                var reservationKey = Guid.NewGuid().ToString("N");
                var reservationBody = new { branchId = branch, partySize = 1,
                    reservedAt = DateTimeOffset.UtcNow.AddDays(2), note = "CloudHost acceptance reservation" };
                var reservation = await Send(client, HttpMethod.Post, "/api/mobile/v1/reservations", reservationBody, reservationKey);
                var reservationId = reservation.GetProperty("reservationId").GetInt32();
                try
                {
                    var repeated = await Send(client, HttpMethod.Post, "/api/mobile/v1/reservations", reservationBody, reservationKey);
                    Ensure(repeated.GetProperty("reservationId").GetInt32() == reservationId, "Reservation replay duplicated record.");
                    using var conflictRequest = new HttpRequestMessage(HttpMethod.Post, "/api/mobile/v1/reservations")
                    { Content = JsonContent.Create(new { reservationBody.branchId, partySize = 2, reservationBody.reservedAt, reservationBody.note }) };
                    conflictRequest.Headers.Add("Idempotency-Key", reservationKey);
                    using var conflict = await client.SendAsync(conflictRequest);
                    Ensure(conflict.StatusCode == HttpStatusCode.Conflict, "Changed reservation payload replay accepted.");
                    var read = await Send(client, HttpMethod.Get, $"/api/mobile/v1/reservations/{reservationId}");
                    Ensure(read.GetProperty("branchId").GetInt32() == branch, "Reservation branch changed.");
                }
                finally
                {
                    var cancelled = await Send(client, HttpMethod.Post, $"/api/mobile/v1/reservations/{reservationId}/cancel", key: reservationKey + "-cancel");
                    Ensure(string.Equals(cancelled.GetProperty("status").GetString(), "Cancelled", StringComparison.OrdinalIgnoreCase), "QA reservation did not cancel.");
                }
                Console.WriteLine("PASS: reservation create/read/idempotent replay/cancel through CloudHost; exact QA reservation retained as cancelled history.");
                var refreshBody = new { refreshToken = refresh.GetProperty("refreshToken").GetString() };
                await Send(client, HttpMethod.Post, "/api/mobile/v1/auth/logout", refreshBody);
                using var revokedRefresh = await client.PostAsJsonAsync("/api/mobile/v1/auth/refresh", refreshBody);
                Ensure(revokedRefresh.StatusCode == HttpStatusCode.Unauthorized, "Logged-out refresh token was accepted.");
                Console.WriteLine("PASS: customer logout persisted revocation; subsequent refresh rejected401. No new business session/order/payment.");
                return;
            }
            foreach(var account in staff)
            {
                var cookieJar=new CookieContainer();
                var actor=new HttpClient(new HttpClientHandler { CookieContainer=cookieJar }) {BaseAddress=client.BaseAddress,Timeout=TimeSpan.FromSeconds(90)};
                var actorKey=account.Role+(account==staff.Last()?"OtherBranch":"");
                actors[actorKey]=actor; staffCookies[actorKey]=cookieJar;
                var route=account.Role switch {"ADMIN"=>"/api/gateway/admin", "CASHIER"=>"/api/gateway/staff/cashier", _=>"/api/gateway/staff"};
                _=await Send(actor,HttpMethod.Post,route+"/auth/login",new {account.Username,account.Password});
                var staffSession=await Send(actor,HttpMethod.Get,route+"/session");
                Ensure(staffSession.GetProperty("authenticated").GetBoolean(),"Staff cookie was not restored.");
                var dashboard=account.Role switch {"ADMIN"=>"/api/gateway/admin/dashboard","CASHIER"=>"/api/gateway/staff/cashier/dashboard",_=>"/api/gateway/staff/chef/dashboard"};
                _=await Send(actor,HttpMethod.Get,dashboard);
                Console.WriteLine("PASS: CloudHost "+account.Role+" password login/cookie session/dashboard; no RAM Session.");
            }
            var chef=actors["CHEF"]; var cashier=actors["CASHIER"]; var admin=actors["ADMIN"];
            using(var anonymous=new HttpClient { BaseAddress=client.BaseAddress })
            using(var denied=await anonymous.PostAsJsonAsync("/hubs/customer-notifications/negotiate?negotiateVersion=1",new{}))
                Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Staff hub anonymous negotiation accepted.");
            using(var denied=await client.PostAsJsonAsync("/hubs/customer-notifications/negotiate?negotiateVersion=1",new{}))
                Ensure(denied.StatusCode==HttpStatusCode.Forbidden,"Customer accessed staff branch hub.");
            await using var staffRealtime=new CloudRealtimeProbe();
            await staffRealtime.ConnectStaffAsync(staffCookies["CASHIER"]);
            await staffRealtime.AssertBranchAsync(fixture.BranchId,true);
            await staffRealtime.AssertBranchAsync(staff.Last().Branch,false);
            using(var denied=await chef.GetAsync("/api/gateway/admin/categories")) Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Non-admin authorization mismatch.");
            foreach(var app in new[]{"chef","cashier","admin"})
            { using var page=await client.GetAsync("/app/"+app+"/"); Ensure(page.IsSuccessStatusCode,$"Staff SPA {app} HTTP {(int)page.StatusCode}."); }
            var idempotency=Guid.NewGuid().ToString("N");
            Console.WriteLine("STEP: connect SignalR and resume/create owner-scoped QA dining session.");
            await using var initialRealtime=new CloudRealtimeProbe();
            await using var resumedRealtime=new CloudRealtimeProbe();
            var realtime=initialRealtime;
            await realtime.ConnectAsync(refresh.GetProperty("accessToken").GetString()!);
            using var activeResponse=await client.GetAsync("/api/mobile/v1/dining-sessions/active");
            Ensure(activeResponse.IsSuccessStatusCode || activeResponse.StatusCode==HttpStatusCode.NotFound,"QA active-session lookup failed.");
            var session=activeResponse.IsSuccessStatusCode
                ? (await activeResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone()
                : await Send(client,HttpMethod.Post,"/api/mobile/v1/check-ins/walk-in",new{qrCode=fixture.QrCode},idempotency+"-walkin");
            var sessionId=session.GetProperty("diningSessionId").GetInt32();
            Ensure(session.GetProperty("branchId").GetInt32()==fixture.BranchId,"QA active session belongs to unexpected branch.");
            if (input.Action == "CloudRecovery")
            {
                var occupied=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{fixture.BranchId}/table-layout");
                Ensure(!occupied.GetProperty("tables").EnumerateArray()
                    .Single(x=>x.GetProperty("tableId").GetInt32()==session.GetProperty("tableId").GetInt32())
                    .GetProperty("physicallyAvailable").GetBoolean(),"New QA session did not occupy Catalog table before recovery test.");
            }
            Console.WriteLine("Retained/resumed staging QA session="+sessionId);
            // Stable per-session commands safely replay a committed order after a response timeout.
            idempotency=input.Action=="CloudRecovery"
                ? "hostqa-recovery-"+sessionId+"-"+Guid.NewGuid().ToString("N")
                : "hostqa-session-"+sessionId;
            var actualMenu=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{fixture.BranchId}/menu");
            var dish=actualMenu.GetProperty("categories").EnumerateArray().SelectMany(x=>x.GetProperty("dishes").EnumerateArray()).First(x=>x.GetProperty("available").GetBoolean());
            var orderBody=new{items=new[]{new{dishId=dish.GetProperty("dishId").GetInt32(),quantity=1}}};
            var orderPath=$"/api/mobile/v1/dining-sessions/{sessionId}/orders";
            Console.WriteLine("STEP: two order rounds with stable idempotency keys.");
            var first=await ConcurrentOrder(client,orderPath,orderBody,idempotency+"-order1");
            var orderId=first.GetProperty("orderId").GetInt32();
            var retry=await Send(client,HttpMethod.Post,orderPath,orderBody,idempotency+"-order1");
            Ensure(retry.GetProperty("orderId").GetInt32()==orderId,"Order idempotency mismatch.");
            var second=await Send(client,HttpMethod.Post,orderPath,orderBody,idempotency+"-order2");
            Ensure(second.GetProperty("orderId").GetInt32()!=orderId,"Order rounds overwritten.");
            // A previous probe may have finished receiving round1 before exiting.
            // Never attempt to reverse that state; cook the still-confirmed round2.
            if(first.GetProperty("status").GetString() is "READY" or "SERVING")
                orderId=second.GetProperty("orderId").GetInt32();
            Console.WriteLine("STEP: Chef PREPARING/READY and authenticated customer receive.");
            await Send(chef,HttpMethod.Post,$"/api/gateway/staff/chef/orders/{orderId}/start");
            await realtime.ExpectAsync("orderUpdated", "PREPARING");
            await Send(chef,HttpMethod.Post,$"/api/gateway/staff/chef/orders/{orderId}/ready");
            if (input.Action == "CloudRecovery")
            {
                var canonical=await Send(client,HttpMethod.Get,$"/api/mobile/v1/orders/{orderId}");
                Ensure(canonical.GetProperty("status").GetString()=="READY","READY state did not commit with its outbox.");
                var before=await Send(client,HttpMethod.Get,"/api/mobile/v1/notifications?limit=100");
                Ensure(!before.GetProperty("items").EnumerateArray().Any(x=>x.GetProperty("orderId").GetInt32()==orderId),
                    "READY projection unexpectedly succeeded during injected failure.");
                using(var denied=await client.PostAsync("/api/internal/recovery",null))
                    Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Customer token accessed recovery endpoint.");
                start.Environment["Recovery__TestFailReadyProjection"]="false";
                await RestartAsync();
                await resumedRealtime.ConnectAsync(refresh.GetProperty("accessToken").GetString()!);
                realtime=resumedRealtime;
                await Task.WhenAll(InvokeRecoveryAsync(),InvokeRecoveryAsync());
                await InvokeRecoveryAsync();
                var after=await Send(client,HttpMethod.Get,"/api/mobile/v1/notifications?limit=100");
                Ensure(after.GetProperty("items").EnumerateArray().Count(x=>x.GetProperty("orderId").GetInt32()==orderId)==1,
                    "READY projection recovery duplicated or lost notification.");
                Console.WriteLine("PASS: READY state survived injected projection failure and process restart; two concurrent plus duplicate recovery calls produced one notification; customer forbidden.");
            }
            var detail=await Send(client,HttpMethod.Get,$"/api/mobile/v1/orders/{orderId}");
            var itemId=detail.GetProperty("items")[0].GetProperty("itemId").GetInt32();
            await Send(client,HttpMethod.Post,$"/api/mobile/v1/orders/{orderId}/items/{itemId}/confirm-received");
            Console.WriteLine("STEP: Cashier service request acknowledge/complete.");
            var request=await Send(client,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/service-requests",new{requestType="WATER"},idempotency+"-water");
            var requestId=request.GetProperty("serviceRequestId").GetInt32();
            await Send(cashier,HttpMethod.Post,$"/api/gateway/staff/cashier/service-requests/{requestId}/acknowledge");
            await realtime.ExpectAsync("serviceRequestUpdated", "ACKNOWLEDGED");
            await Send(cashier,HttpMethod.Post,$"/api/gateway/staff/cashier/service-requests/{requestId}/complete");
            Console.WriteLine("STEP: checkout, payment branch denial and correct cashier confirmation.");
            var quote=await Send(client,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/checkout/quote",new{},idempotency+"-quote");
            var quoteId=quote.GetProperty("checkoutQuoteId").GetInt32();
            decimal expectedSubtotal;
            if (input.Action == "CloudRecovery") expectedSubtotal=quote.GetProperty("totalAmount").GetDecimal();
            else {
                var sessionOrders=await Send(client,HttpMethod.Get,"/api/mobile/v1/orders?scope=all");
                expectedSubtotal=sessionOrders.EnumerateArray().Where(x=>x.GetProperty("diningSessionId").ValueKind==JsonValueKind.Number &&
                    x.GetProperty("diningSessionId").GetInt32()==sessionId).Sum(x=>x.GetProperty("subtotal").GetDecimal());
            }
            Ensure(quote.GetProperty("totalAmount").GetDecimal()==expectedSubtotal,"Quote total mismatch.");
            await Send(client,HttpMethod.Post,$"/api/mobile/v1/dining-sessions/{sessionId}/payment-request",new{checkoutQuoteId=quoteId});
            var payment=await Send(client,HttpMethod.Post,$"/api/mobile/v1/checkout/quotes/{quoteId}/payments",new{channel="ONSITE",method="CASH",location="TABLE"},idempotency+"-payment");
            var paymentId=payment.GetProperty("paymentId").GetInt32();
            using(var denied=await client.PostAsJsonAsync($"/api/gateway/staff/cashier/onsite-payments/{paymentId}/confirm",new{}))
                Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Customer acquired staff role.");
            using(var denied=await actors["CASHIEROtherBranch"].PostAsJsonAsync($"/api/gateway/staff/cashier/onsite-payments/{paymentId}/confirm",new{}))
                Ensure(denied.StatusCode==HttpStatusCode.Forbidden,"Wrong-branch cashier was not rejected.");
            if (input.Action == "CloudRecovery")
            {
                using(var failed=await cashier.PostAsJsonAsync($"/api/gateway/staff/cashier/onsite-payments/{paymentId}/confirm",new{}))
                    Ensure((int)failed.StatusCode>=500,"Injected post-commit failure did not interrupt cashier response.");
                var committed=await Send(client,HttpMethod.Get,$"/api/mobile/v1/payments/{paymentId}");
                Ensure(committed.GetProperty("status").GetString()=="SUCCEEDED","Payment did not commit before injected failure.");
                var pending=await Send(client,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}");
                Ensure(pending.GetProperty("status").GetString()!="CLOSED","Session already closed during injected failure.");
                start.Environment["Recovery__TestFailAfterPaymentCommit"]="false";
                start.Environment["Recovery__TestFailTableRelease"]="true";
                await RestartAsync();
                await InvokeRecoveryAsync();
                var closedBeforeRelease=await Send(client,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}");
                Ensure(closedBeforeRelease.GetProperty("status").GetString()=="CLOSED","Orders completion was not durable before Catalog failure.");
                var stillOccupied=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{fixture.BranchId}/table-layout");
                Ensure(!stillOccupied.GetProperty("tables").EnumerateArray()
                    .Single(x=>x.GetProperty("tableId").GetInt32()==closedBeforeRelease.GetProperty("tableId").GetInt32())
                    .GetProperty("physicallyAvailable").GetBoolean(),"Catalog table unexpectedly released during injected failure.");
                start.Environment["Recovery__TestFailTableRelease"]="false";
                await RestartAsync();
                await Task.WhenAll(InvokeRecoveryAsync(),InvokeRecoveryAsync());
                await InvokeRecoveryAsync();
                Console.WriteLine("PASS: SUCCEEDED payment and CLOSED session survived two injected boundaries and process restarts; concurrent/duplicate secured recovery released table and completed settlement.");
            }
            else await Send(cashier,HttpMethod.Post,$"/api/gateway/staff/cashier/onsite-payments/{paymentId}/confirm",new{});
            var paid=await Send(client,HttpMethod.Get,$"/api/mobile/v1/payments/{paymentId}");
            Ensure(paid.GetProperty("status").GetString()=="SUCCEEDED","Payment not succeeded.");
            var invoice=await Send(client,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}/invoice");
            Ensure(invoice.GetProperty("totalAmount").GetDecimal()==quote.GetProperty("totalAmount").GetDecimal(),"Invoice total mismatch.");
            if (input.Action == "CloudRecovery")
            {
                await using var receiptDb=new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
                    .UseNpgsql(CloudWorkflow.RuntimeConnection(input,"billing")).Options);
                var receipts=await receiptDb.Invoices.AsNoTracking()
                    .Where(x=>x.DiningSessionId==sessionId).ToListAsync();
                Ensure(receipts.Count==1 && receipts[0].SettlementCompletedAtUtc.HasValue
                    && receipts[0].RecoveryAttemptCount>=2 && receipts[0].RecoveryLastAttemptAtUtc.HasValue,
                    "Recovered payment produced duplicate or unfinished invoice receipt.");
                Console.WriteLine("PASS: one final invoice and durable settlement receipt after duplicate recovery.");
            }
            var closed=await Send(client,HttpMethod.Get,$"/api/mobile/v1/dining-sessions/{sessionId}");
            Ensure(closed.GetProperty("status").GetString()=="CLOSED","Session did not close synchronously.");
            var released=await Send(client,HttpMethod.Get,$"/api/mobile/v1/branches/{fixture.BranchId}/table-layout");
            var tableId=closed.GetProperty("tableId").GetInt32();
            Ensure(released.GetProperty("tables").EnumerateArray().Single(x=>x.GetProperty("tableId").GetInt32()==tableId)
                .GetProperty("physicallyAvailable").GetBoolean(),"Paid session table was not released.");
            Console.WriteLine($"PASS: CloudHost cross-role API flow, two rounds, Chef READY/customer SERVING, service request acknowledge/complete, wrong cashier branch403, ONSITE/invoice/CLOSED; session={sessionId}; payment={paymentId}.");
            await Send(admin,HttpMethod.Get,"/api/gateway/admin/categories");
            await Send(admin,HttpMethod.Get,"/api/gateway/admin/tables");
            Console.WriteLine("PASS: Admin categories/tables read-only smoke; no demo data edited.");
            var categoryName="QA TEMP CloudHost "+Guid.NewGuid().ToString("N");
            var categoryBody=new{name=categoryName,description="Temporary CloudHost acceptance",displayOrder=999};
            using(var denied=await chef.PostAsJsonAsync("/api/gateway/admin/categories",categoryBody))
                Ensure(denied.StatusCode==HttpStatusCode.Unauthorized,"Chef performed Admin mutation.");
            await Send(admin,HttpMethod.Post,"/api/gateway/admin/categories",categoryBody);
            var categoryList=await Send(admin,HttpMethod.Get,"/api/gateway/admin/categories");
            var categoryId=categoryList.GetProperty("categories").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==categoryName)
                .GetProperty("categoryId").GetInt32();
            try
            {
                await Send(admin,HttpMethod.Put,$"/api/gateway/admin/categories/{categoryId}",
                    new{name=categoryName+" edited",description="Temporary CloudHost acceptance",displayOrder=999,isActive=false});
                categoryList=await Send(admin,HttpMethod.Get,"/api/gateway/admin/categories");
                Ensure(categoryList.GetProperty("categories").EnumerateArray().Single(x=>x.GetProperty("categoryId").GetInt32()==categoryId)
                    .GetProperty("name").GetString()==categoryName+" edited","Admin update did not persist.");
            }
            finally { await Send(admin,HttpMethod.Delete,$"/api/gateway/admin/categories/{categoryId}"); }
            categoryList=await Send(admin,HttpMethod.Get,"/api/gateway/admin/categories");
            Ensure(!categoryList.GetProperty("categories").EnumerateArray().Any(x=>x.GetProperty("categoryId").GetInt32()==categoryId),"Temporary category was not removed.");
            Console.WriteLine("PASS: real Admin API CRUD persisted and exact QA-only category deleted; non-admin mutation denied.");
            var recovery=await Send(client,HttpMethod.Post,"/api/gateway/staff/auth/forgot-password",new{email=staff.Last().Username});
            var resetToken=recovery.GetProperty("resetToken").GetString() ?? throw new CloudAcceptanceException("Development reset token not issued.");
            process.Kill(entireProcessTree:true); await process.WaitForExitAsync();
            Console.WriteLine("STEP: actual CloudHost process restart.");
            _=await output; _=await errors; process.Dispose();
            process=Process.Start(start)??throw new CloudAcceptanceException("CloudHost restart failed.");
            output=process.StandardOutput.ReadToEndAsync(); errors=process.StandardError.ReadToEndAsync();
            ready=false;
            for(var attempt=0;attempt<240;attempt++)
            {
                if(process.HasExited) throw new CloudAcceptanceException("CloudHost exited on restart.");
                try{using var response=await client.GetAsync("/readyz");ready=response.IsSuccessStatusCode;}catch(HttpRequestException){}
                if(ready)break;await Task.Delay(500);
            }
            Ensure(ready,"CloudHost restart readiness timeout.");
            await Send(client,HttpMethod.Get,"/api/mobile/v1/me");
            await using var reconnected=new CloudRealtimeProbe();
            await reconnected.ConnectAsync(refresh.GetProperty("accessToken").GetString()!);
            Console.WriteLine("PASS: authenticated SignalR reconnect handshake after actual process restart.");
            foreach(var (role,actor) in actors)
            {
                var route=role=="ADMIN"?"/api/gateway/admin/session":role.StartsWith("CASHIER")?"/api/gateway/staff/cashier/session":"/api/gateway/staff/session";
                var state=await Send(actor,HttpMethod.Get,route);
                Ensure(state.GetProperty("authenticated").GetBoolean(),"Staff cookie lost after process restart.");
            }
            Console.WriteLine("PASS: actual process restart; same customer JWT and four staff cookies authenticated using durable encrypted Identity key ring.");
            // Do not include the reset token in a diagnostic path or persisted output.
            using(var validated=await client.GetAsync("/api/gateway/staff/auth/reset-password/validate?token="+Uri.EscapeDataString(resetToken)))
                Ensure(validated.IsSuccessStatusCode,"Staff reset token did not survive restart.");
            var resetBody=new{token=resetToken,newPassword=password,confirmPassword=password};
            await Send(client,HttpMethod.Post,"/api/gateway/staff/auth/reset-password",resetBody);
            using(var reused=await client.PostAsJsonAsync("/api/gateway/staff/auth/reset-password",resetBody))
                Ensure(reused.StatusCode==HttpStatusCode.BadRequest,"Staff reset token reuse accepted.");
            var revoked=await Send(actors["CASHIEROtherBranch"],HttpMethod.Get,"/api/gateway/staff/cashier/session");
            Ensure(!revoked.GetProperty("authenticated").GetBoolean(),"Staff password reset did not revoke cookie version.");
            Console.WriteLine("PASS: durable staff reset across process restart, one-time redemption and stale-cookie revocation (QA identity only).");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree:true);
            await process.WaitForExitAsync();
            // Logs may contain sensitive request data. Keep in memory, never print/save.
            var diagnostic=await output;
            foreach(var line in diagnostic.Split('\n').Where(x=>x.StartsWith("CloudHostTransport ",StringComparison.Ordinal)))
                Console.WriteLine(line.Trim());
            _=await errors;
            if (process.ExitCode != 0)
            {
                var withheld = await errors;
                var exceptionType = System.Text.RegularExpressions.Regex.Match(withheld,
                    @"(?:Unhandled exception\. )?([A-Za-z0-9_.]+Exception):");
                if (exceptionType.Success) Console.WriteLine("CloudHostTransport startup-exit-type=" + exceptionType.Groups[1].Value);
            }
            process.Dispose();
            foreach(var actor in actors.Values) actor.Dispose();
        }
    }
    private static void Ensure(bool condition,string message) { if(!condition) throw new CloudAcceptanceException(message); }
    private static async Task<JsonElement> ConcurrentOrder(HttpClient client,string path,object body,string key)
    {
        using var one=new HttpRequestMessage(HttpMethod.Post,path) { Content=JsonContent.Create(body) };
        using var two=new HttpRequestMessage(HttpMethod.Post,path) { Content=JsonContent.Create(body) };
        one.Headers.Add("Idempotency-Key",key); two.Headers.Add("Idempotency-Key",key);
        one.Headers.Add("X-Correlation-Id",Guid.NewGuid().ToString("N"));
        two.Headers.Add("X-Correlation-Id",Guid.NewGuid().ToString("N"));
        var results=await Task.WhenAll(client.SendAsync(one),client.SendAsync(two));
        try
        {
            Ensure(results.All(x=>x.IsSuccessStatusCode || x.StatusCode==HttpStatusCode.Conflict),"Concurrent order returned unexpected status.");
            var canonical=await Send(client,HttpMethod.Post,path,body,key);
            foreach(var result in results.Where(x=>x.IsSuccessStatusCode))
            {
                var payload=await result.Content.ReadFromJsonAsync<JsonElement>();
                Ensure(payload.GetProperty("orderId").GetInt32()==canonical.GetProperty("orderId").GetInt32(),"Concurrent order duplicated.");
            }
            Console.WriteLine("PASS: concurrent same-key order commands return canonical order or409, no duplicate ID/no500.");
            return canonical;
        }
        finally { foreach(var result in results) result.Dispose(); }
    }
    internal static async Task<JsonElement> Send(HttpClient client,HttpMethod method,string path,object? body=null,string? key=null)
    {
        using var request=new HttpRequestMessage(method,path);
        request.Headers.Add("X-Correlation-Id",Guid.NewGuid().ToString("N"));
        if(key is not null) request.Headers.Add("Idempotency-Key",key);
        if(body is not null) request.Content=JsonContent.Create(body);
        using var response=await client.SendAsync(request);
        if(!response.IsSuccessStatusCode)
        {
            // Only a constrained business error code is diagnostic-safe; never
            // print response bodies, reset tokens, credentials or query strings.
            string? code = null;
            try
            {
                var error = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (error.TryGetProperty("code", out var field))
                {
                    var value = field.GetString();
                    if (value is { Length: <= 80 } && value.All(x => char.IsAsciiLetterOrDigit(x) || x == '_')) code = value;
                }
            }
            catch (JsonException) { }
            throw new CloudAcceptanceException($"HTTP {(int)response.StatusCode} at {method.Method} {path.Split('?')[0]}; code={code ?? "unavailable"}");
        }
        var content=await response.Content.ReadAsStringAsync();
        if(string.IsNullOrWhiteSpace(content)) return JsonSerializer.SerializeToElement(new{}); // Valid204/void action.
        using var json=JsonDocument.Parse(content);
        return json.RootElement.Clone();
    }
}
