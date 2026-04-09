mv StardewAIMod.csproj StardewAIMod.csproj.bak
cat << 'CSPROJ' > StardewAIMod.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <EnableHarmony>true</EnableHarmony>
    <BundleExtraAssemblies>ThirdParty</BundleExtraAssemblies>
  </PropertyGroup>
</Project>
CSPROJ
dotnet build
mv StardewAIMod.csproj.bak StardewAIMod.csproj
