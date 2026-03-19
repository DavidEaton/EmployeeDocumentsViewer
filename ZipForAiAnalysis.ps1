param(
    [string]$SolutionName = "EmployeeDocumentsViewer",
    [string]$SolutionRoot = "C:\code\$SolutionName",
    # Where to write the output ZIP
    [string]$OutputZip = "C:\Dev\$SolutionName-For-Ai-Analysis.zip"
)

Write-Host "Solution root: $SolutionRoot"
Write-Host "Output zip   : $OutputZip"
Write-Host ""

# Normalize and validate paths
$SolutionRoot = (Resolve-Path $SolutionRoot).ProviderPath.TrimEnd('\')
if (-not (Test-Path $SolutionRoot)) {
    Write-Error "SolutionRoot path does not exist: $SolutionRoot"
    exit 1
}

# Directories to exclude anywhere in the tree
$excludeDirFragments = @(
    '\.git\',
    '\.vs\',
    '\bin\',
    '\obj\',
    '\node_modules\',
    '\packages\',
    '\wwwroot\lib\',
    '\wwwroot\dist\'
)

# File extensions we actually care about
$allowedExtensions = @(
    '.sln',
    '.csproj',
    '.fsproj',
    '.vbproj',
    '.cs',
    '.razor',
    '.cshtml',
    '.config',
    '.json',
    '.yml',
    '.yaml',
    '.xml',
    '.props',
    '.targets',
    '.md',
    '.ts',
    '.js',
    '.css',
    '.scss'
)

Write-Host "Collecting files..."

$files = Get-ChildItem -Path $SolutionRoot -Recurse -File | Where-Object {
    $full = $_.FullName

    # Exclude any file under unwanted directories
    $inExcludedDir = $false
    foreach ($frag in $excludeDirFragments) {
        if ($full -like "*$frag*") {
            $inExcludedDir = $true
            break
        }
    }

    if ($inExcludedDir) { return $false }

    # Filter by extension to keep archive small
    $ext = [System.IO.Path]::GetExtension($_.Name)
    return $allowedExtensions -contains $ext
}

if (-not $files -or $files.Count -eq 0) {
    Write-Warning "No files matched the filters. Check SolutionRoot and filters."
    exit 1
}

Write-Host ("Files to include: {0}" -f $files.Count)

# Ensure output folder exists
$outputDir = [System.IO.Path]::GetDirectoryName($OutputZip)
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Remove old ZIP if it exists
if (Test-Path $OutputZip) {
    Write-Host "Removing existing archive at $OutputZip..."
    Remove-Item $OutputZip
}

Write-Host "Creating archive..."
Compress-Archive -Path $files.FullName -DestinationPath $OutputZip -Force

Write-Host ""
Write-Host "Done."
Write-Host "Created archive:"
Write-Host "  $OutputZip"
