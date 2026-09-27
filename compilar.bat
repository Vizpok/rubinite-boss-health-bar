@echo off
rem Genera dist\BossHealthBar.exe (requiere: pip install pyinstaller dnfile==0.18.0)
cd /d "%~dp0"
python -m PyInstaller --noconfirm --onefile --console --name BossHealthBar --hidden-import dnfile barra_jefes.py
pause
