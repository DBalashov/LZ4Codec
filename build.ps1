$CD = $PSScriptRoot

cd $CD\LZ4Codec

& "dotnet" @("build", "--configuration", "Release")

cd $CD

nuget pack LZ4Codec.nuspec