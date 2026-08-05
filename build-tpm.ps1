$base    = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
. (Join-Path $base "..\Shared\BuildTpm.Common.ps1")

Build-AbrTpm -Base $base -TpmName "Runoff" `
    -DllPath "bin\Debug\net48\Abr.Runoff.dll" `
    -PluginFiles @("Runoff.plugin", "t_runoff_tab.plugin") `
    -NeedsSetupExe
