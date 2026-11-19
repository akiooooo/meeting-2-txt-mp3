$ErrorActionPreference = 'Stop'
$modelDir = Join-Path (Split-Path $PSScriptRoot) 'models'
[IO.Directory]::CreateDirectory($modelDir) | Out-Null
$models = @(
    @{ File = 'ggml-small-q5_1.bin'; Repo = 'ggerganov/whisper.cpp'; Hash = 'ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb' }
    @{ File = 'ggml-silero-v6.2.0.bin'; Repo = 'ggml-org/whisper-vad'; Hash = '2aa269b785eeb53a82983a20501ddf7c1d9c48e33ab63a41391ac6c9f7fb6987' }
)
foreach ($model in $models) {
    $path = Join-Path $modelDir $model.File
    if ((Test-Path -LiteralPath $path) -and (Get-FileHash -LiteralPath $path).Hash -eq $model.Hash) {
        Write-Output "Ready: $($model.File)"
        continue
    }
    $partial = "$path.$([guid]::NewGuid()).partial"
    try {
        Write-Output "Downloading $($model.File)..."
        $url = "https://huggingface.co/$($model.Repo)/resolve/main/$($model.File)"
        curl.exe --fail --location --silent --show-error --retry 2 --output $partial $url
        if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $partial).Hash -ne $model.Hash) {
            throw "Could not verify $($model.File). Run the script again."
        }
        Move-Item -LiteralPath $partial -Destination $path -Force
    } finally {
        if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial }
    }
}
