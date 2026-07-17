# SHOP_SCREEN_V1

## Purpose

`SHOPUI-001` adds the functional Hub Shop presentation over the existing SHOP-001, MON-001, INV-001, GEN-001, and RAP-001 authorities. The screen does not own a wallet, inventory, stock list, sold flag, price calculation, reward grant, or transaction outcome.

## Authority boundary

`ShopScreenSessionV1` is a thin application/presentation adapter. It receives the already composed runtime authorities and immutable Hub route payload, then:

1. calls `ShopRuntimeServiceV1.Open` to obtain the deterministic run/shop inventory;
2. projects `ShopInventoryViewV1` and `MoneyWalletService.Balance` into immutable UI cards;
3. constructs `ShopPurchaseCommandV1` from the exact authority stock identity, inventory fingerprint, claimant, and price;
4. submits purchases only through `ShopRuntimeServiceV1.Purchase`;
5. reopens the authority view after each result so sold and pending states are never cached as local truth.

There are no direct calls to `MoneyWalletService.Spend`, `MoneyWalletService.Grant`, holdings mutation, or reward application from the screen layer.

## Deterministic stock and duplicate definitions

Each card exposes both:

- the equipment definition identity; and
- the concrete immutable equipment-instance identity.

Two stock entries may therefore display the same definition while remaining different purchasable instances. The card key is the SHOP stock-entry identity, not the definition identity.

Prices, stock-entry identities, equipment-instance identities, inventory fingerprints, sold state, and pending state are projected exactly from SHOP-001.

## Purchase input and retry rules

A normal **BUY** action creates a deterministic presentation input identity and submits it once.

A pending entry carries the purchase transaction identity recorded by SHOP-001. **RETRY PENDING PURCHASE** reuses that exact identity. It does not create a second operation. SHOP-001 therefore retains responsibility for:

- exact duplicate replay with no additional value;
- conflicting duplicate rejection;
- one money spend;
- one equipment application;
- pending RAP continuation;
- refund/compensation continuation;
- sold-state transition only after confirmed application.

## Scene integration

Scene:

`Assets/ShooterMover/Scenes/Flow/Shop/Shop.unity`

Before loading it, the composition root should create a real `ShopScreenSessionV1` and call:

```csharp
ShopScreenRuntimeHandoffV1.Prepare(session, routeAdapter);
```

The handoff is one-shot and carries authority references, not copied shop data. Tests or embedded flows may instead call `ShopScreenControllerV1.Configure` directly.

The route adapter receives `ShopScreenRouteV1.Hub` together with the exact incoming `PlayerRouteProfilePayloadV1` object. Back input is locked after the first emitted route, so repeated callbacks cannot emit multiple transitions or mutate the payload.

## Backplate and controls

`Assets/ShooterMover/Art/UI/Shop/shop_template.png` is a non-authoritative visual backplate. The original named artwork was not present on the dispatch-time `main` branch or in the available upload library, so this change includes a neutral replaceable backplate at the required path.

All stock cards, values, status labels, BUY/RETRY controls, and Back control are real IMGUI overlays. Replacing the PNG changes appearance only; it cannot alter prices, balance, stock, sold state, or purchase behavior.

## Feedback mapping

The screen explicitly projects:

- purchase success;
- exact duplicate no-change replay;
- conflicting duplicate rejection;
- insufficient funds;
- sold state;
- purchase pending;
- compensation/refund pending;
- stale inventory or price rejection;
- generic authority rejection.

The authority fact is retained on `ShopScreenActionResultV1` for diagnostics and tests.

## Validation expectations

Focused EditMode coverage verifies deterministic projection, prices, duplicate definitions as separate instances, successful authority purchase, insufficient funds, exact/conflicting duplicate input, pending retry, and immutable Hub return.

Focused PlayMode coverage verifies controller purchase projection, duplicate replay, retry using the pending authority identity, and one-shot Back routing.

Do not claim Unity execution proof unless XML test results show zero failures. Static inspection alone is not Unity test proof.
