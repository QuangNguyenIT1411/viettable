using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text.Json;
using SelfRestaurant.Catalog.Api;
using SelfRestaurant.Identity.Api;
using VietTable.CloudHost.Composition;

internal sealed record AcceptanceStaff(int Id,string Username,string Password,string Role,int Branch);
internal sealed record AcceptanceFixture(List<AcceptanceStaff> Staff,string QrCode,int TableId,int BranchId);

internal static class CloudStaffProvisioner
{
    // Explicit acceptance-tool bootstrap via existing Identity management API pipeline.
    // Not a public CloudHost route, not production startup seeding, no SQL INSERT.
    public static async Task<AcceptanceFixture> CreateAsync(Input input)
    {
        var router=new ModuleRouter(); var apps=new List<WebApplication>();
        try
        {
            foreach(var owner in new[]{"Catalog","Identity"})
            {
                var server=new ModuleServer(); router.Register(owner,server);
                var options=new WebApplicationOptions { ApplicationName="SelfRestaurant."+owner+".Api",EnvironmentName="Production" };
                void Configure(WebApplicationBuilder builder)
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?> {
                        ["Database:Provider"]="Postgres",["Database:ApplyMigrations"]="false",["Database:SeedProfile"]="None",
                        ["ConnectionStrings:"+owner+"Db"]=CloudWorkflow.RuntimeConnection(input,owner.ToLowerInvariant()),
                        ["MobileJwt:SigningKey"]=input.CloudHostSigningKey,["Logging:LogLevel:Default"]="None",
                        ["Runtime:LegacyPersistentWorkers"]="false",["HttpsRedirection:Enabled"]="false",
                        ["Services:Catalog"]="http://Catalog.module.invalid" });
                    builder.Services.AddSingleton<IServer>(server);
                    builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new ModuleClientFilter(router));
                }
                var app=owner=="Catalog"?await CatalogModule.BuildAsync([],options,Configure):await IdentityModule.BuildAsync([],options,Configure);
                apps.Add(app);await app.StartAsync();
            }
            using var catalog=new HttpClient(new ModuleDispatchHandler(router){InnerHandler=new HttpClientHandler()}){BaseAddress=new Uri("http://Catalog.module.invalid")};
            using var identity=new HttpClient(new ModuleDispatchHandler(router){InnerHandler=new HttpClientHandler()}){BaseAddress=new Uri("http://Identity.module.invalid")};
            var branches=await CloudHostAcceptance.Send(catalog,HttpMethod.Get,"/api/branches");
            var roles=await CloudHostAcceptance.Send(identity,HttpMethod.Get,"/api/identity/admin/roles");
            var result=new List<AcceptanceStaff>();
            foreach(var (role,index) in new[]{("ADMIN",0),("CHEF",0),("CASHIER",0),("CASHIER",1)})
            {
                var branch=branches[index].GetProperty("branchId").GetInt32();
                var roleId=roles.EnumerateArray().Single(x=>x.GetProperty("roleCode").GetString()==role).GetProperty("roleId").GetInt32();
                var username="hostqa_"+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input.CloudHostAcceptancePassword)))[..24].ToLowerInvariant()+role.ToLowerInvariant()+index;
                var password=input.CloudHostAcceptancePassword;
                var found=await CloudHostAcceptance.Send(identity,HttpMethod.Get,"/api/identity/admin/employees?search="+username);
                var existing=found.GetProperty("items").EnumerateArray().Where(x=>x.GetProperty("username").GetString()==username).ToArray();
                var created=existing.Length==1?existing[0]:await CloudHostAcceptance.Send(identity,HttpMethod.Post,"/api/identity/admin/employees",new {
                    name="CloudHost role acceptance",username,password,isActive=true,branchId=branch,roleId });
                var id=created.GetProperty("employeeId").GetInt32();
                result.Add(new(id,username,password,role,branch));
                Console.WriteLine($"Retained acceptance staff ID={id}; role={role}; branch={branch}. Credential not displayed.");
            }
            var primary=result[0].Branch;
            var layout=await CloudHostAcceptance.Send(catalog,HttpMethod.Get,$"/api/branches/{primary}/tables");
            var table=layout.GetProperty("tables").EnumerateArray().First(x=>x.GetProperty("isAvailable").GetBoolean());
            return new(result,table.GetProperty("qrCode").GetString()!,table.GetProperty("tableId").GetInt32(),primary);
        }
        finally {foreach(var app in apps.AsEnumerable().Reverse()){await app.StopAsync();await app.DisposeAsync();}}
    }
}
