#!/usr/bin/env node
/**
 * Record mock-up walkthrough (Playwright + Chromium) and export MP4 + GIF.
 *
 * Prerequisites:
 *   npm install
 *   npx playwright install chromium
 *
 * Usage (from this directory):
 *   npm run render
 */
import { chromium } from "playwright";
import { spawnSync } from "child_process";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const dir = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(dir, "..");
const htmlPath = path.join(dir, "walkthrough.html");
const mp4Path = path.join(outDir, "mockup-walkthrough.mp4");
const gifPath = path.join(outDir, "mockup-walkthrough.gif");
const tmpDir = path.join(dir, ".tmp");

function run(cmd, args) {
  const r = spawnSync(cmd, args, { encoding: "utf8" });
  if (r.status !== 0) {
    console.error(r.stderr || r.stdout);
    throw new Error(`${cmd} exited ${r.status}`);
  }
  return r.stdout;
}

function ensureFfmpeg() {
  const r = spawnSync("ffmpeg", ["-version"], { encoding: "utf8" });
  if (r.status !== 0) {
    throw new Error("ffmpeg is required on PATH");
  }
}

async function main() {
  ensureFfmpeg();
  fs.mkdirSync(tmpDir, { recursive: true });
  fs.mkdirSync(outDir, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 1,
    recordVideo: {
      dir: tmpDir,
      size: { width: 1280, height: 720 },
    },
  });
  const page = await context.newPage();
  const url = "file://" + htmlPath;
  await page.goto(url, { waitUntil: "networkidle" });
  await page.evaluate(() => window.runWalkthrough());
  await page.waitForFunction(() => window.__walkthroughDone === true, {
    timeout: 120_000,
  });
  await page.waitForTimeout(300);

  const video = page.video();
  await context.close();
  await browser.close();

  const webmPath = await video.path();
  console.log("Recorded", webmPath);

  run("ffmpeg", [
    "-y",
    "-i",
    webmPath,
    "-c:v",
    "libx264",
    "-crf",
    "28",
    "-preset",
    "medium",
    "-pix_fmt",
    "yuv420p",
    "-movflags",
    "+faststart",
    mp4Path,
  ]);

  run("ffmpeg", [
    "-y",
    "-i",
    mp4Path,
    "-vf",
    "fps=8,scale=800:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128[p];[s1][p]paletteuse=dither=bayer",
    "-loop",
    "0",
    gifPath,
  ]);

  try {
    fs.unlinkSync(webmPath);
  } catch {
    /* ignore */
  }

  const mp4Size = fs.statSync(mp4Path).size;
  const gifSize = fs.statSync(gifPath).size;
  console.log(`Wrote ${mp4Path} (${(mp4Size / 1024 / 1024).toFixed(2)} MiB)`);
  console.log(`Wrote ${gifPath} (${(gifSize / 1024 / 1024).toFixed(2)} MiB)`);

  if (mp4Size > 10 * 1024 * 1024) {
    console.warn("Warning: MP4 exceeds 10 MiB target");
  }
  if (gifSize > 5 * 1024 * 1024) {
    console.warn("Warning: GIF exceeds 5 MiB target");
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
