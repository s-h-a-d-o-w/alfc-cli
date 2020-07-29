SCHTASKS /Create /F /DELAY 0001:00 /TN "Aorus Fan Control (Login)" /SC ONLOGON /TR "%CD%\alfc %CD%\%1" /RL HIGHEST
SCHTASKS /Create /F /DELAY 0001:00 /TN "Aorus Fan Control (After Hibernation)" /SC ONEVENT /EC System /MO "*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]" /TR "%CD%\alfc %CD%\%1" /RL HIGHEST
