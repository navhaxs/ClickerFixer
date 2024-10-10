
sudo mkdir /opt/clicker-fixer-app/

```
sudo systemctl start clicker.service
sudo systemctl stop clicker.service
sudo systemctl restart clicker.service
sudo systemctl status clicker.service
sudo journalctl --unit clicker.service --follow
```

/etc/systemd/system/clicker.service
```
[Unit]
Description=ClickerFixerApp
After=network.target

[Service]
ExecStart=/opt/clicker-fixer-app/ClickerFixer.Satellite
Restart=always
User=pi

[Install]
WantedBy=multi-user.target
```

sudo systemctl start clicker.service