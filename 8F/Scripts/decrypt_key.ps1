$b64 = (Get-Content "C:\ProgramData\Eddy\8F\config.key").Trim()
$cipherBytes = [Convert]::FromBase64String($b64)
$aes = [System.Security.Cryptography.Aes]::Create()
$aes.Key = [System.Text.Encoding]::UTF8.GetBytes("12345678901234567890123456789012")
$aes.IV = [System.Text.Encoding]::UTF8.GetBytes("1234567890123456")
$aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
$aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
$dec = $aes.CreateDecryptor()
$plainBytes = $dec.TransformFinalBlock($cipherBytes, 0, $cipherBytes.Length)
[System.Text.Encoding]::UTF8.GetString($plainBytes)
