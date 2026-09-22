$paths = @(
  'C:\Program Files\dotnet\dotnet.exe',
  'C:\Program Files (x86)\dotnet\dotnet.exe',
  'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'
)
foreach($p in $paths){
  if(Test-Path $p){ Write-Output "FOUND $p" } else { Write-Output "MISS  $p" }
}
