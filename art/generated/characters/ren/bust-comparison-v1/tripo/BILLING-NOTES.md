# Tripo API funding check

Checked 2026-09-13. The BWS `TRIPO_API_KEY` authenticated against the v3 balance endpoint. At the time of the initial check it returned `balance: 0.0`, `frozen: 0.0`. No generation was attempted to obtain that result, and no key value was displayed or persisted.

## Approved batch estimate

| Setting | Credits per job | Jobs | Credits |
| --- | ---: | ---: | ---: |
| H3.1 multiview with texture: 30; Ultra geometry: +20; extreme texture: +20 | 70 | 2 | 140 |
| P2 with extreme texture | 130 | 2 | 260 |
| Total | | 4 | **400** |

The listed conversion is 100 credits per US dollar, so generation totals **US$4.00 at list price**, before account discounts or additional processing. H-series quad/additional-processing charges are not part of these H requests. The H addon list is presented separately from the P-series table. [Official pricing](https://developers.tripo3d.ai/en/pricing). The August P2 changelog gives 130 credits with extreme textures; it does not list a separate P2 quad surcharge. [Official changelog](https://developers.tripo3d.ai/en/docs/changelog).

This is prepaid credit usage: required credits are reserved at task creation, then consumed on success or released on failure/cancellation. The pay-as-you-go description does not establish permission to generate with zero credits. [Official billing](https://developers.tripo3d.ai/en/docs/billing). Insufficient credits are documented as error 2010, with console top-up as the remedy. [Official error handling](https://developers.tripo3d.ai/en/docs/error-handling).

## Where to fund

The Help Center's API top-up link resolves to `https://developers.tripo3d.ai/zh/billing`; the English route is `https://developers.tripo3d.ai/en/billing`. Studio subscription credits cannot pay for API requests. [Official explanation and top-up link](https://www.tripo3d.ai/help/api-plugins/tripo-studiotripo-api).

The public JavaScript delivered by the current official developer site (`/assets/index-B60rXeOI.js`, inspected read-only on 2026-09-13) implements a custom whole-dollar amount with a $1 minimum; quick choices are $50, $100, and $250. Thus **custom $4** is the smallest amount covering the four listed generations exactly; $5 would leave 100 credits for later format conversion or another small operation. The signed-in checkout remains authoritative for currency, tax, account discounts, and available payment methods. No subscription or automatic-recharge setting was changed by this investigation.
