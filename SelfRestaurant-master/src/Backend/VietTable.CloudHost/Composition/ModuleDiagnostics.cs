namespace VietTable.CloudHost.Composition;

public static class ModuleDiagnostics
{
    public static void Configure(WebApplication app) => app.Use(async (context,next) => {
        try { await next(); }
        catch(Exception error) {
            var cause=error.GetBaseException();
            Console.WriteLine("CloudHostTransport module-error-type="+cause.GetType().Name);
            if(cause is Npgsql.PostgresException postgres)
                Console.WriteLine("CloudHostTransport SQLSTATE="+postgres.SqlState);
            foreach(var frame in new System.Diagnostics.StackTrace(cause).GetFrames()?.Where(x=>
                x.GetMethod()?.DeclaringType?.FullName?.StartsWith("SelfRestaurant.")==true).Take(5) ?? [])
                Console.WriteLine("CloudHostTransport module-location="+frame.GetMethod()?.DeclaringType?.FullName+"."+frame.GetMethod()?.Name);
            throw;
        }
    });
}
