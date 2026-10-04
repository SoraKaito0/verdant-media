# Verdant Patreon Identity — V12.2.0

Verdant now recognises five Patreon ranks from `config/supporters.json`:

- Tier 1 Basic — $3/month
- Tier 2 Supporter — $6/month
- Tier 3 VIP — $10/month
- Tier 4 Elite — $20/month
- Tier 5 Guardian — $30/month

## Add a supporter

Edit `config/supporters.json` and add an entry to the `supporters` array using their exact VRChat display name:

```json
{
  "name": "VRChat Display Name",
  "tier": "elite",
  "colour": "",
  "joinTitle": ""
}
```

Valid tier keys are `basic`, `supporter`, `vip`, `elite`, and `guardian`.

Elite and Guardian members can choose their own supporter name colour and optional 30-character join title inside the Supporter Board. Those choices are stored by VRChat PlayerData and are only honoured when the player is also listed in the remote supporter file at Tier 4 or Tier 5.

Patreon rank does **not** grant staff permissions. Staff roles remain controlled by the Verdant staff/role system.

## Assets

Patreon badges are in `badges/icons/`. Backgrounds have been sorted under `assets/backgrounds/` by minimum required tier. `config/backgrounds.json` is prepared for the future remote background loader.
