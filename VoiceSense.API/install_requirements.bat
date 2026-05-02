@echo off
cd /d "%~dp0"
echo VoiceSense API Gereksinimleri Yukleniyor...
"C:\Users\Esat\AppData\Local\Programs\Python\Python311\python.exe" -m pip install -r requirements.txt
echo.
echo Kurulum Tamamlandi! API'yi baslatmak icin run_api.bat dosyasini calistirabilirsiniz.
pause
