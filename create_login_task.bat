SCHTASKS /Create /F /DELAY 0001:00 /TN "Aorus Fan Control" /SC ONLOGON /TR "%CD%\afc %CD%\%1" /RL HIGHEST
