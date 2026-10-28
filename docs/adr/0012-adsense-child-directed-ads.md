# ADR 0012 — Google AdSense, child-directed, with Google's consent message

**Status:** Accepted (owner, 2026-10-27)

## Context
The live site pays for itself with banner ads in fixed, reserved slots (FR-50, FR-51); until now they were placeholders behind a provider switch (FR-52). Families and classrooms play, so many players are children: in the US, COPPA forbids personalised ads to children under 13 without parental consent. Visitors in the EU, the UK and Switzerland must agree before a site or its ad network stores anything on their device, and Google requires a Google-certified consent tool (IAB TCF) to serve ads to them.

Networks considered:
- **Google AdSense** accepts a new site with little traffic, after a site review.
- Contextual or privacy-first networks (Media.net, Ezoic, Mediavine, Carbon, EthicalAds) need traffic thresholds or developer-focused content.
- Kid-safe networks (SuperAwesome, Kidoz) are aimed at apps and large publishers.

## Decision
- **Network:** Google AdSense, on the live (Lean) site only. The Full portfolio build and local development keep the placeholders.
- **Every ad request is tagged for child treatment** (`data-tag-for-age-treatment="1"`, which replaced the older child-directed tag). That means no personalised ads, no remarketing and no third-party ad requests. It earns less, and it's the safe position for a classroom audience. `AdSlot` adds the tag itself, so no slot can leave it out.
- **Consent in the EU, UK and Switzerland:** Google's own consent message, set up in the AdSense account (Privacy & messaging) and shown by AdSense's script. To change a choice later there's a footer link, **Privacy and cookie settings**, instead of Google's floating button, which would sit over the page.
- **The one exception to "no pop-ups" (FR-51):** that consent message. The law requires it, and it appears only on a first visit from those regions, before any ad. Everything else stays as before:
  - banners in fixed slots;
  - no overlays, interstitials or anchor ads, which means **Auto ads stay off** in the AdSense account;
  - no ads on the TV display;
  - ads asked for once per slot and never refreshed on a timer.
- **Switches, all build-time and none secret** (the ids appear in every page's source):
  - `VITE_ADSENSE_CLIENT` (the publisher id, in `web/.env`) adds Google's site-verification tag to the page and writes `ads.txt`, which AdSense's review needs before any ad shows. A malformed id fails the build.
  - The two ad units, `VITE_ADSENSE_SLOT_BANNER` (320 × 50) and `VITE_ADSENSE_SLOT_TOWER` (160 × 600), are also in `web/.env`.
  - `VITE_AD_PROVIDER=adsense` turns ads on. The live image's CI build reads it from the repository variable `AD_PROVIDER`, so ads can be switched on or off without a code change.
  - Without all of these, the slots stay placeholders and nothing from Google loads.
- **The Privacy page describes the build it's part of.** With ads on, it names Google AdSense, says what Google receives (IP address, browser details, cookies for frequency capping and fraud), says that every ad is requested as child-directed, and explains how to change consent, linking Google's own explanation.

## Consequences
- **One third party.** Until now, every request went to SyncBingo only. With ads, browsers also talk to Google, which sees IP addresses and may set cookies. The Privacy page says so, and the planned legal review matters more now.
- **Lower earnings.** Child-directed, non-personalised ads earn noticeably less than personalised ones. That's accepted.
- **AdSense may turn the site down** as having too little content, since it is mostly an app. The landing page may need more explanatory text to pass the review.
- **No content security policy to loosen.** The site has none; if one is added later, it must allow Google's ad and consent domains.
- **Testing:**
  - Unit tests check the switches (placeholders by default, and with a missing id), the child-treatment tag on every unit, that only the visible pair of slots asks for ads, the fixed sizes, no ads on the TV, the consent link, the Privacy text and `ads.txt`.
  - The end-to-end suite keeps running with placeholders, so it never calls Google.
