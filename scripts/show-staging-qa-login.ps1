[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Cashier', 'Admin', 'Chef', 'Customer')]
    [string]$Role,
    [switch]$Personal,
    [switch]$District1,
    [switch]$CheckOnly,
    [switch]$VerifyUi
)

# Local manual handoff only: no network, browser injection or account mutation.
# Password never goes to stdout, clipboard, arguments or a plaintext file.
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Windows current-user DPAPI is required.' }
$qaUsers = @{
    Cashier = 'hostqa_8c074c45aa3ed29c022249edcashier0'
    Admin = 'hostqa_8c074c45aa3ed29c022249edadmin0'
    Chef = 'hostqa_8c074c45aa3ed29c022249edchef0'
    Customer = 'q1uat_20261012'
}
if ($Role -eq 'Customer' -and ($Personal -or $District1)) { throw 'Customer uses the isolated Q1 QA account; omit Personal/District1.' }
if ($Personal) {
    if ($Role -eq 'Chef') { throw 'Personal Chef has not been provisioned; use District1 or existing QA.' }
    $qaUsers = @{ Cashier='quang_cashier_staging'; Admin='quang_admin_staging' }
}
if ($District1) {
    if ($Personal) { throw 'Choose District1 or Personal, not both.' }
    $qaUsers = @{ Cashier='cashier_lan'; Admin='admin'; Chef='chef_hung' }
}
$qaDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'VietTable\staging-secrets'
$qaSecretName = if ($Personal -or $District1) { $qaUsers[$Role] } else { 'CloudHostAcceptancePassword' }
$qaFile = Join-Path $qaDirectory ($qaSecretName + '.dpapi')
if (-not (Test-Path -LiteralPath $qaFile -PathType Leaf)) {
    throw 'QA DPAPI credential is missing. No account or password was changed.'
}
$qaSecure = $null
$qaPointer = [IntPtr]::Zero
$qaForm = $null
$qaTimer = $null
$qaPlain = $null
$qaPasswordBox = $null
try {
    try {
        $qaSecure = ConvertTo-SecureString ([IO.File]::ReadAllText($qaFile))
        if ($qaSecure.Length -eq 0) { throw 'Empty credential.' }
    } catch { throw 'QA DPAPI credential cannot be opened by this Windows user.' }
    if ($CheckOnly) {
        Write-Output "PASS: $Role local DPAPI credential is readable. No secret displayed."
        return
    }
    if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
        throw 'Run with Windows PowerShell -STA. No secret displayed.'
    }
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $qaPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($qaSecure)
    $qaPlain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($qaPointer)
    $qaForm = New-Object Windows.Forms.Form
    $qaAccountLabel = if ($District1) { 'District 1' } elseif ($Personal) { 'Personal' } else { 'QA' }
    $qaForm.Text = "VietTable staging $qaAccountLabel - $Role - LOCAL ONLY"
    $qaForm.ClientSize = New-Object Drawing.Size(570, 245)
    $qaForm.StartPosition = 'CenterScreen'
    $qaForm.FormBorderStyle = 'FixedDialog'
    $qaForm.MaximizeBox = $false
    $qaInfo = New-Object Windows.Forms.Label
    $qaInfo.Text = "Manual login only. Do not share your screen or take a screenshot.`r`nThe password stays on this PC. No clipboard/password logging."
    $qaInfo.SetBounds(15, 12, 540, 45)
    $qaForm.Controls.Add($qaInfo)
    $qaUserBox = New-Object Windows.Forms.TextBox
    $qaUserBox.ReadOnly = $true
    $qaUserBox.Text = $qaUsers[$Role]
    $qaUserBox.SetBounds(15, 65, 540, 28)
    $qaForm.Controls.Add($qaUserBox)
    $qaPasswordBox = New-Object Windows.Forms.TextBox
    $qaPasswordBox.ReadOnly = $true
    $qaPasswordBox.UseSystemPasswordChar = $true
    $qaPasswordBox.ShortcutsEnabled = $false
    $qaPasswordBox.ContextMenuStrip = New-Object Windows.Forms.ContextMenuStrip
    $qaPasswordBox.Text = $qaPlain
    $qaPasswordBox.SetBounds(15, 108, 540, 28)
    $qaForm.Controls.Add($qaPasswordBox)
    $qaTimer = New-Object Windows.Forms.Timer
    $qaTimer.Interval = 10000
    $qaTimer.Add_Tick({ $qaPasswordBox.UseSystemPasswordChar = $true; $qaTimer.Stop() })
    $qaReveal = New-Object Windows.Forms.Button
    $qaReveal.Text = 'Show password locally for 10 seconds'
    $qaReveal.SetBounds(15, 152, 340, 32)
    $qaReveal.Add_Click({ $qaPasswordBox.UseSystemPasswordChar = $false; $qaTimer.Stop(); $qaTimer.Start() })
    $qaForm.Controls.Add($qaReveal)
    $qaClose = New-Object Windows.Forms.Button
    $qaClose.Text = 'Close'
    $qaClose.SetBounds(410, 152, 145, 32)
    $qaClose.Add_Click({ $qaForm.Close() })
    $qaForm.Controls.Add($qaClose)
    $qaNote = New-Object Windows.Forms.Label
    $qaNote.Text = 'Copy username if needed; type the password manually into the trusted Preview.'
    $qaNote.SetBounds(15, 202, 540, 30)
    $qaForm.Controls.Add($qaNote)
    if ($VerifyUi) {
        if (-not $qaPasswordBox.UseSystemPasswordChar -or $qaPasswordBox.ShortcutsEnabled -or
            $qaUserBox.Text -ne $qaUsers[$Role]) { throw 'Local handoff safety check failed.' }
        Write-Output "PASS: $Role handoff controls initialized masked; no window or secret displayed."
        return
    }
    [void]$qaForm.ShowDialog()
} finally {
    if ($qaTimer) { $qaTimer.Stop(); $qaTimer.Dispose() }
    if ($qaForm) {
        if ($qaPasswordBox) { $qaPasswordBox.Clear() }
        $qaForm.Dispose()
    }
    $qaPlain = $null
    if ($qaPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($qaPointer) }
    if ($qaSecure) { $qaSecure.Dispose() }
}
