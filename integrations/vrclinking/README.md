# Verdant + VRCLinking Hybrid Integration

This integration keeps Verdant Bot/manual role data as the fallback while allowing VRCLinking to become the preferred VRChat/Discord identity and role provider once its World Sync data loads successfully.

## Required VRCLinking setup

Install the VRCLinking SDK through VCC using:

`https://vpm.vrclinking.com/index.json`

Then in Unity:

1. Wait for Unity and UdonSharp to finish compiling.
2. Open the Verdant world scene.
3. Run:
   **Tools > Verdant Board > Integrations > VRCLinking > Setup Hybrid Bridge**
4. The first run generates a bridge specifically for the installed VRCLinking SDK API.
5. Wait for compilation again.
6. Run **Setup Hybrid Bridge** a second time.
7. Configure the generated **VRCLinking Downloader** in its Inspector by logging in and selecting the Discord server and Verdant world.
8. In VRCLinking World Sync, enable **Always Encode Roles**.

Recommended encoded role names:

- Member
- Basic
- Supporter
- VIP
- Elite
- Guardian
- Moderator
- Admin
- Manager
- Owner

These names are editable on the generated Verdant VRCLinking Bridge component.

## Role mapping

Supporter ranks:

- Basic = 1
- Supporter = 2
- VIP = 3
- Elite = 4
- Guardian = 5

Staff ranks:

- Moderator = 1
- Admin = 2
- Manager = 3
- Owner = 4

A user may have both a supporter tier and a staff rank at the same time.

## Fallback behaviour

Until VRCLinking successfully loads data, Verdant does not overwrite existing manual/GitHub role state.

Once VRCLinking successfully loads, the encoded VRCLinking roles become authoritative for the mapped supporter/staff role values.

## Validation

Use:

**Tools > Verdant Board > Integrations > VRCLinking > Validate Hybrid Bridge**

to check whether the SDK, downloader, generated bridge, role authority and discovered Verdant targets are present.

## Safety

The installer makes a scene backup before it wires the bridge into the active scene.
