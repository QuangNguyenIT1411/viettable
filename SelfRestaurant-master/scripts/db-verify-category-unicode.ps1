param([string]$ConnectionString='Server=(localdb)\MSSQLLocalDB;Database=RESTAURANT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True')
$ErrorActionPreference='Stop'
$conn=New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
$conn.Open()
try{
  $cmd=$conn.CreateCommand();
  $cmd.CommandText='SELECT CategoryID, Name, Description FROM Categories WHERE CategoryID BETWEEN 1 AND 4 ORDER BY CategoryID'
  $r=$cmd.ExecuteReader()
  while($r.Read()){
    $id=$r.GetInt32(0); $name=[string]$r.GetValue(1); $desc=[string]$r.GetValue(2)
    $nameCodes = ($name.ToCharArray() | ForEach-Object {[int][char]$_}) -join ','
    $descCodes = ($desc.ToCharArray() | ForEach-Object {[int][char]$_}) -join ','
    Write-Output "ID=$id"
    Write-Output "NAME=$name"
    Write-Output "NAME_CODES=$nameCodes"
    Write-Output "DESC=$desc"
    Write-Output "DESC_CODES=$descCodes"
    Write-Output '---'
  }
  $r.Close()
}
finally{$conn.Close()}
