@echo off
rem Genera BossHealthBar.exe. Necesita Mono.Cecil.dll (0.11.x, net40) en esta carpeta;
rem se distribuye junto al .exe (nada va incrustado).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" -nologo -codepage:65001 -optimize+ -target:exe -out:BossHealthBar.exe -r:Mono.Cecil.dll src\BossHealthBar.cs || exit /b 1
echo Listo: BossHealthBar.exe (distribuir junto con Mono.Cecil.dll)