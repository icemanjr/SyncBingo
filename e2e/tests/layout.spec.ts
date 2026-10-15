import { expect, test, type Locator } from "@playwright/test";
import { hostGame, joinGame } from "./game";

const box = async (locator: Locator) => {
  const b = await locator.boundingBox();
  if (!b) throw new Error("not visible");
  return b;
};
const overlaps = (
  a: { x: number; y: number; width: number; height: number },
  b: typeof a,
) =>
  a.x < b.x + b.width &&
  b.x < a.x + a.width &&
  a.y < b.y + b.height &&
  b.y < a.y + a.height;

test("on a phone, the ad banners keep their reserved size and never cover or move the card", async ({
  browser,
}) => {
  const { code } = await hostGame(browser);
  const ava = await joinGame(browser, code, "Ava");
  const card = ava.getByRole("grid", { name: "Your card" });
  const ads = ava.getByRole("complementary", { name: "Advertisement" });

  await expect(ads).toHaveCount(2);
  const before = await box(card);
  for (const ad of await ads.all()) {
    const slot = await box(ad);
    expect([slot.width, slot.height]).toEqual([320, 50]);
    expect(overlaps(slot, before)).toBe(false);
  }

  await ava.waitForTimeout(1000); // anything that loads late (fonts, a real ad) must not shift the card
  expect(await box(card)).toEqual(before);
});

test("on a laptop, the host console has an ad rail on each side", async ({
  browser,
}) => {
  const { host } = await hostGame(browser);

  const ads = host.getByRole("complementary", { name: "Advertisement" });
  await expect(ads).toHaveCount(2);
  for (const ad of await ads.all()) {
    const slot = await box(ad);
    expect([slot.width, slot.height]).toEqual([160, 600]);
  }
});
