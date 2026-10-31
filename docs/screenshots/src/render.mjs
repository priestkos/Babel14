import { chromium } from "playwright";
import { fileURLToPath } from "url";
import path from "path";

const dir = path.dirname(fileURLToPath(import.meta.url));
const pages = [
  ["01-translate-tab.html", "../01-translate-tab.png"],
  ["02-settings-tab.html", "../02-settings-tab.png"],
  ["03-incoming-chat.html", "../03-incoming-chat.png"],
  ["04-babel-send-command.html", "../04-babel-send-command.png"],
];

const browser = await chromium.launch();
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  deviceScaleFactor: 1,
});
const page = await context.newPage();

for (const [html, out] of pages) {
  const url = "file://" + path.join(dir, html);
  await page.goto(url, { waitUntil: "networkidle" });
  await page.screenshot({
    path: path.join(dir, out),
    type: "png",
  });
  console.log("Wrote", out);
}

await browser.close();
