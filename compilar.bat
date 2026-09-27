@echo off
rem Genera dist\BarraJefes.exe (requiere: pip install pyinstaller dnfile==0.18.0)
cd /d "%~dp0"
python -m PyInstaller --noconfirm --onefile --console --name BarraJefes --hidden-import dnfile barra_jefes.py
pause
