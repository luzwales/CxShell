# External Login and Xshell Compatibility

CxShell can be launched by a bastion host, SSO portal, or another local tool using a small Xshell-compatible surface. The request is converted to one in-memory external launch model before it reaches the normal connection workflow.

## Supported forms

```text
CxShell.exe -url ssh://user@server.example:22
CxShell.exe -url sftp://user@server.example:22/home/user
CxShell.exe -l user -p 2222 -pw one-time-password -url ssh://server.example
CxShell.exe -i C:\keys\id_ed25519 -url ssh://user@server.example
CxShell.exe -f C:\Temp\server.xsh
```

The short options `-user`, `-port`, `-password`, and `-identity` are accepted as aliases. A bare `ssh://` or `sftp://` URL is also accepted. Explicit command-line values override values in the URL.

CxShell intentionally does not implement Xshell remote-command options such as `-e` or `-s`. External links should open a session, not silently execute an arbitrary command on a server.

## Security behavior

- External login is controlled by Application Settings > External login / Xshell compatibility.
- Confirmation is enabled by default. The dialog shows source, protocol, target, and whether a credential was supplied, but never shows the password.
- Trusted targets are scoped to protocol, username, host, and port. Passwords and private-key passphrases are never part of the trust key.
- A password supplied by a bastion or SSO tool is kept in memory for the connection attempt and is not saved to `sessions.json`.
- External requests are recorded in the local connection audit without the command line or password.
- The `ssh://` and `sftp://` associations are disabled by default and use only the current user's Windows registry or Linux desktop files.

## Xshell session files

`-f` reads the UTF-16 INI fields from an `.xsh` file: host, port, protocol, username, and private-key path. Xshell's encrypted password is not decrypted. If no usable credential is supplied, CxShell uses its normal in-app authentication prompt.

## Platform notes

- Windows writes `HKCU\Software\Classes\ssh` and `HKCU\Software\Classes\sftp`; administrator permission is not required.
- Linux writes `~/.local/share/applications/cxshell-url-handler.desktop` and uses `xdg-mime` when available.
- macOS URL registration is not changed at runtime because scheme declarations belong in the application bundle's `Info.plist`.
