#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Low')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [ValidateNotNull()]
    [System.Security.SecureString]$SigningPassword,

    [Parameter(Mandatory = $true)]
    [ValidateNotNull()]
    [System.Security.SecureString]$EncryptionPassword,

    [ValidateRange(2048, 8192)]
    [int]$KeyLength = 3072,

    [ValidateRange(30, 825)]
    [int]$ValidityDays = 365
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

foreach ($password in @($SigningPassword, $EncryptionPassword)) {
    if ($password.Length -lt 16) {
        throw 'Each PFX password must contain at least 16 characters.'
    }
}

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory) -or
    $OutputDirectory -notmatch '^(?:[A-Za-z]:[\\/]|\\\\[^\\/]+[\\/][^\\/]+[\\/])') {
    throw 'OutputDirectory must be a fully qualified drive or UNC path outside the repository.'
}

$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$outputRoot = [System.IO.Path]::GetPathRoot($resolvedOutputDirectory)
if ([string]::Equals($resolvedOutputDirectory.TrimEnd('\', '/'), $outputRoot.TrimEnd('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be a folder below the drive or network share root.'
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\', '/')
$repositoryPrefix = $repositoryRoot + [System.IO.Path]::DirectorySeparatorChar
if ([string]::Equals($resolvedOutputDirectory.TrimEnd('\', '/'), $repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $resolvedOutputDirectory.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Certificate output must be outside the repository.'
}

$signingPath = Join-Path $resolvedOutputDirectory 'openid-signing.pfx'
$encryptionPath = Join-Path $resolvedOutputDirectory 'openid-encryption.pfx'
$manifestPath = Join-Path $resolvedOutputDirectory 'manifest.json'
$targetFiles = @($signingPath, $encryptionPath, $manifestPath)
foreach ($path in $targetFiles) {
    if (Test-Path -LiteralPath $path) {
        throw "Refusing to overwrite an existing file: $path"
    }
}

if (-not $PSCmdlet.ShouldProcess($resolvedOutputDirectory, 'Create local OpenIddict test certificates')) {
    return
}

$createdDirectory = $false
$createdFiles = New-Object 'System.Collections.Generic.List[string]'
$certificates = New-Object 'System.Collections.Generic.List[System.Security.Cryptography.X509Certificates.X509Certificate2]'
$rsaKeys = New-Object 'System.Collections.Generic.List[System.Security.Cryptography.RSA]'

function ConvertTo-TemporaryPlainText {
    param([System.Security.SecureString]$SecureValue)

    $securePointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($securePointer)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($securePointer)
    }
}

try {
    if (-not (Test-Path -LiteralPath $resolvedOutputDirectory)) {
        New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
        $createdDirectory = $true
    }

    $notBefore = (Get-Date).AddMinutes(-5)
    $notAfter = (Get-Date).AddDays($ValidityDays)
    $certificateDefinitions = @(
        [pscustomobject]@{
            Name = 'signing'
            Subject = 'CN=JobApplicationAgent Local OpenIddict Signing'
            KeyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature
            Path = $signingPath
            Password = $SigningPassword
        },
        [pscustomobject]@{
            Name = 'encryption'
            Subject = 'CN=JobApplicationAgent Local OpenIddict Encryption'
            KeyUsage = [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment
            Path = $encryptionPath
            Password = $EncryptionPassword
        }
    )

    $manifestCertificates = New-Object 'System.Collections.Generic.List[object]'
    foreach ($definition in $certificateDefinitions) {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $rsa.KeySize = $KeyLength
        $rsaKeys.Add($rsa)

        $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $definition.Subject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
        )
        $request.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new($definition.KeyUsage, $true)
        )
        $certificate = $request.CreateSelfSigned($notBefore, $notAfter)

        $certificates.Add($certificate)
        if (-not $certificate.HasPrivateKey -or $rsa.KeySize -lt 2048) {
            throw "The generated $($definition.Name) certificate does not meet the RSA private-key requirements."
        }

        $passwordText = ConvertTo-TemporaryPlainText -SecureValue $definition.Password
        $pfxBytes = $null
        $pfxStream = $null
        try {
            $pfxBytes = $certificate.Export(
                [System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx,
                $passwordText
            )
            $pfxStream = [System.IO.File]::Open(
                $definition.Path,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None
            )
            $createdFiles.Add($definition.Path)
            $pfxStream.Write($pfxBytes, 0, $pfxBytes.Length)
        }
        finally {
            if ($pfxStream) {
                $pfxStream.Dispose()
            }
            $passwordText = $null
            if ($pfxBytes) {
                [Array]::Clear($pfxBytes, 0, $pfxBytes.Length)
            }
        }

        $pfxData = Get-PfxData -FilePath $definition.Path -Password $definition.Password
        $exportedCertificate = @($pfxData.EndEntityCertificates | Where-Object { $_.Thumbprint -eq $certificate.Thumbprint } | Select-Object -First 1)
        if ($exportedCertificate.Count -ne 1) {
            throw "The exported $($definition.Name) PFX did not contain the generated certificate."
        }

        $verificationPassword = ConvertTo-TemporaryPlainText -SecureValue $definition.Password
        $verificationBytes = $null
        $verifiedCertificate = $null
        try {
            $verificationBytes = [System.IO.File]::ReadAllBytes($definition.Path)
            $verifiedCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
                $verificationBytes,
                $verificationPassword,
                [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet
            )
            $verifiedUsage = @($verifiedCertificate.Extensions | Where-Object { $_ -is [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension] } | Select-Object -First 1)
            if (-not $verifiedCertificate.HasPrivateKey -or
                $verifiedCertificate.Thumbprint -ne $certificate.Thumbprint -or
                $verifiedCertificate.PrivateKey.KeySize -lt 2048 -or
                $verifiedUsage.Count -ne 1 -or
                ([int]$verifiedUsage[0].KeyUsages -band [int]$definition.KeyUsage) -ne [int]$definition.KeyUsage) {
                throw "The exported $($definition.Name) PFX failed its private-key or usage validation."
            }
        }
        finally {
            if ($verifiedCertificate) {
                $verifiedCertificate.Dispose()
            }
            if ($verificationBytes) {
                [Array]::Clear($verificationBytes, 0, $verificationBytes.Length)
            }
            $verificationPassword = $null
        }

        $manifestCertificates.Add([pscustomobject]@{
            purpose = $definition.Name
            file = [System.IO.Path]::GetFileName($definition.Path)
            thumbprint = $certificate.Thumbprint
            rsaKeyLength = $rsa.KeySize
            keyUsage = $definition.KeyUsage.ToString()
            notBeforeUtc = $certificate.NotBefore.ToUniversalTime().ToString('o')
            notAfterUtc = $certificate.NotAfter.ToUniversalTime().ToString('o')
            sha256 = (Get-FileHash -LiteralPath $definition.Path -Algorithm SHA256).Hash
        })
    }

    $manifest = [pscustomobject]@{
        formatVersion = 1
        generatedAtUtc = [DateTime]::UtcNow.ToString('o')
        intendedUse = 'Local or staging testing only; not production certificate provisioning.'
        certificates = @($manifestCertificates.ToArray())
    }
    $json = $manifest | ConvertTo-Json -Depth 5
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    $manifestBytes = $utf8WithoutBom.GetBytes($json + [Environment]::NewLine)
    $manifestStream = $null
    try {
        $manifestStream = [System.IO.File]::Open(
            $manifestPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None
        )
        $createdFiles.Add($manifestPath)
        $manifestStream.Write($manifestBytes, 0, $manifestBytes.Length)
    }
    finally {
        if ($manifestStream) {
            $manifestStream.Dispose()
        }
        [Array]::Clear($manifestBytes, 0, $manifestBytes.Length)
    }

    Write-Output "Created local OpenIddict test certificates in '$resolvedOutputDirectory'."
    Write-Output 'PFX passwords were not saved. Protect the output directory and remove the files when no longer needed.'
}
catch {
    foreach ($path in $createdFiles) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }

    if ($createdDirectory -and (Test-Path -LiteralPath $resolvedOutputDirectory) -and
        -not (Get-ChildItem -LiteralPath $resolvedOutputDirectory -Force | Select-Object -First 1)) {
        Remove-Item -LiteralPath $resolvedOutputDirectory -Force
    }

    throw
}
finally {
    foreach ($certificate in $certificates) {
        $certificate.Dispose()
    }

    foreach ($rsa in $rsaKeys) {
        $rsa.Dispose()
    }
}
