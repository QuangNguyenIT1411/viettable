# Local HTTPS

Gateway MVC supports two local launch profiles:

- `http` -> `http://localhost:5100`
- `https` -> `https://localhost:7100;http://localhost:5100`

## Visual Studio

1. Open `SelfRestaurant.Microservices.sln`.
2. Set `SelfRestaurant.Gateway.Api` as startup project if you only want to run the gateway.
3. Choose profile `https`.
4. Start debugging with `F5`.
5. If Visual Studio asks to trust the ASP.NET Core certificate, choose `Yes`.

## dotnet run

Use the .NET SDK installed at `C:\Program Files\dotnet\dotnet.exe` if `dotnet` is not on `PATH`.

```powershell
cd C:\Users\Quang\Downloads\SINH_VIEN\SINH_VIEN\SelfRestaurant-main\src\Gateway\SelfRestaurant.Gateway.Api
& 'C:\Program Files\dotnet\dotnet.exe' dev-certs https --trust
& 'C:\Program Files\dotnet\dotnet.exe' run --launch-profile https
```

## Verification

Quick check with Windows curl:

```powershell
curl.exe -k -I https://localhost:7100
```

Expected result:

- `HTTP/1.1 200 OK`

If `7100` is listening but the browser still warns about the certificate, trust the ASP.NET Core development certificate again and restart the browser.

