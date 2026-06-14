$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
Write-Host "Found MSBuild at: $msbuild"
& $msbuild "d:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI_V2\Bricscad_AgentAI_V2.sln" /t:Restore
& $msbuild "d:\GitHub\Bricscad_AgentAI\Bricscad_AgentAI_V2\Bricscad_AgentAI_V2.sln" /t:Rebuild
