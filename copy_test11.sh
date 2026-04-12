cd test_copy
cat << 'PROJ' > Test.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <Target Name="CopyWhisperNativeLibs" AfterTargets="Build">
    <ItemGroup>
      <WhisperRuntimes Include="$$(TargetDir)runtimes\**\*.*" />
    </ItemGroup>
    <Copy SourceFiles="@(WhisperRuntimes)" DestinationFolder="$$(TargetDir)%(RecursiveDir)" SkipUnchangedFiles="true" />
  </Target>
  <ItemGroup>
    <PackageReference Include="Whisper.net" Version="1.9.0" />
    <PackageReference Include="Whisper.net.Runtime" Version="1.9.0" />
  </ItemGroup>
</Project>
PROJ
dotnet clean
dotnet build -v n | grep "Copying file from"
