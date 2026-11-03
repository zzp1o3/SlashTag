# 发布 SlashTag：GUI 为自包含单文件 exe，MCP 服务为框架依赖单文件 exe。
# 输出到仓库根目录的 dist/。
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    dotnet publish src/SlashTag/SlashTag.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -o dist

    dotnet publish src/SlashTag.Mcp/SlashTag.Mcp.csproj -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -o dist
}
finally {
    Pop-Location
}
