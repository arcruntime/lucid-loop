# iPhone development with a local relay

The selected development setup is an iPhone and relay computer on the same trusted LAN. The current Windows host reported **192.168.1.87** on **Ethernet 3** on September 14, 2026. Enter **`ws://192.168.1.87:8080/game`** in the encounter connection field. The coordinator derives `/live` for NPC conversations. `localhost` on the phone points to the phone, not this computer. Recheck the address after changing networks or DHCP leases.

## Start the relay on Windows

The engineering scene smoke currently runs a separate loopback relay at `ws://127.0.0.1:8789/game`, without a local OpenAI key. It can validate gameplay but cannot provide real NPC voice. The commands below describe starting the LAN relay; change `PORT` and the phone URL together if 8080 is occupied. A GitHub Actions secret is available to CI, not automatically to this local process.

Use Node 24 and a terminal with the project's `OPENAI_API_KEY` already configured for the server process. Do not place that key in Unity, a scene, the iPhone connection form, or source control. The phone uses a separate relay access token:

```powershell
Set-Location B:/lucid-loop/server
$env:HOST = '0.0.0.0'
$env:PORT = '8080'
$env:ACCESS_TOKEN = Read-Host 'Choose a relay access token; enter the same token on the iPhone' -MaskInput
$env:INTENT_MODEL = 'gpt-5.6-luna'
npm ci
npm start
```

The relay requires `ACCESS_TOKEN` when bound beyond loopback. It keeps the OpenAI key on the computer and connects upstream using TLS. The access token and game resume credentials travel over this development LAN WebSocket, so use the trusted test network. A non-development player requires `wss://` for a remote relay.

Find the active host address without displaying credentials:

```powershell
Get-NetIPConfiguration | Where-Object { $_.NetAdapter.Status -eq 'Up' -and $_.IPv4DefaultGateway } |
  Select-Object InterfaceAlias, @{Name='IPv4';Expression={$_.IPv4Address.IPAddress}}
```

Allow inbound TCP 8080 for this relay on the trusted Private network. If a firewall rule is needed, an administrator can add a scoped rule:

```powershell
New-NetFirewallRule -DisplayName 'Lucid Loop local relay' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8080 -Profile Private -RemoteAddress LocalSubnet
```

The phone must be on the same reachable LAN; guest Wi-Fi isolation can block traffic even when both devices have internet access. Do not forward the port through the router. Stop the foreground server with Ctrl+C after testing.

## Connect the development player

1. Export a **Development Build** using [the iOS build workflow](IOS_BUILD.md), then sign/install through Xcode on the Mac. The build flag matters: an Xcode Debug configuration alone does not define Unity's `DEVELOPMENT_BUILD`.
2. On the phone, open the encounter and enter the `/game` URL and the separate access token selected above.
3. Allow Local Network access when iOS asks. If the first connection fails while the prompt is open, allow access and retry manually. iOS can reject the initial operation before the permission decision arrives. Access can be changed under Settings → Privacy & Security → Local Network. [Apple's local-network guidance](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy).
4. Start with a typed conversation. Microphone permission is requested only when microphone input is enabled. Confirm NPC audio, transcript history, one accepted game action, and a clean conversation close.

For a basic reachability check, visit `http://192.168.1.87:8080/health` in the phone's browser. `ready: true` confirms the server has a configured key; it does not validate that key with OpenAI, the WebSocket protocol, or the app's own local-network permission. A 503 response indicates missing server key configuration.

## What the build changes

[RelayAddressPolicy](../Unity/Assets/Gyms/Runtime/RelayAddressPolicy.cs) is shared by both WebSocket transports. Editor and Development Builds allow cleartext `ws://` for RFC1918 IPv4 ranges, IPv4 link-local, IPv6 unique-local/link-local, and `.local` hosts. Release players reject those remote cleartext addresses. Loopback diagnostics and remote `wss://` remain supported. Public cleartext destinations, URL credentials, queries, and fragments are rejected in every mode.

[IosLocalRelayPostprocessor](../Unity/Assets/Gyms/Editor/IosLocalRelayPostprocessor.cs) updates the exported `Info.plist`:

- Adds `NSLocalNetworkUsageDescription` explaining the relay connection.
- For Development Builds, sets `NSAllowsLocalNetworking` and adds insecure-load exceptions only for the private/link-local CIDR ranges supported by the address policy.
- For release exports, disables the local cleartext flag and removes those development CIDR entries, including when reusing an export directory.
- Keeps `NSAllowsArbitraryLoads` false. Direct connections require no Bonjour service list or multicast entitlement.

The project's minimum **iOS 17** matches Apple's documented support for IP-address and CIDR entries in `NSExceptionDomains`; older versions do not support these entries in the same way. [Apple's IP exception documentation](https://developer.apple.com/documentation/bundleresources/information-property-list/nsapptransportsecurity/nsexceptiondomains), [local networking version notes](https://developer.apple.com/documentation/bundleresources/information-property-list/nsapptransportsecurity/nsallowslocalnetworking).

ATS protects Apple's URL Loading System; it does not cover every lower-level networking interface. Unity's managed `ClientWebSocket`/IL2CPP transport must not be assumed to follow `URLSession`'s ATS path. The shared C# address policy enforces this project's scheme/host restriction independently; the scoped plist configuration covers ATS-governed requests without globally disabling it. [Apple's ATS scope](https://developer.apple.com/documentation/security/preventing-insecure-network-connections).

## Verification status

Validate an exported plist with automatic format detection and a value roundtrip:

```powershell
python tools/check_ios_plist.py Unity/Builds/iOS/Xcode/Info.plist --development
```

Omit `--development` when checking a release export. The [checker](../tools/check_ios_plist.py) catches a missing XML declaration, verifies the scoped ATS settings, and does not load the external plist DTD. The serializer now emits a UTF-8 XML declaration and standard plist DOCTYPE. The existing development export was repaired with this serializer; every parsed plist value was verified unchanged.

Direct compilation against the installed Unity 6000.3.24f1 assemblies passed for the transports and iOS postprocessor. Independent development/release checks passed for allowed LAN addresses, rejected public cleartext URLs, TLS/loopback behavior, plist export cleanup, and preservation of unrelated bundle metadata. Versioned [address-policy tests](../Unity/Assets/Gyms/Tests/Editor/RelayAddressPolicyTests.cs) are available to the Unity test suite.

This change did not launch a relay, retrieve a secret, change firewall rules, export/sign an Xcode project, or establish physical iPhone connectivity. Those device checks remain necessary; plist and compile checks alone do not establish working iOS networking.
