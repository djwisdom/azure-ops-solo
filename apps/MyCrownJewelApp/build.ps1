param(
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$Run,
    [switch]$Test
)

$projectPath = "src\MyCrownJewelApp.Pfpad\MyCrownJewelApp.Pfpad.csproj"

if ($Clean) {
    Write-Host "Cleaning..." -ForegroundColor Yellow
    dotnet clean $projectPath
}

Write-Host "Building $Configuration..." -ForegroundColor Green
dotnet build $projectPath -c $Configuration

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green

    if ($Run) {
        Write-Host "Running application..." -ForegroundColor Cyan
        dotnet run --project $projectPath -c $Configuration
    }

    if ($Test) {
        Write-Host "Running tests..." -ForegroundColor Cyan
        dotnet test "tests\MyCrownJewelApp.Tests\MyCrownJewelApp.Tests.csproj" -c $Configuration --no-build
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}