// Gemensamma inställningar för e2e-skripten. Allt kan styras med miljövariabler, se README.md.
const fs = require('fs'), path = require('path'), puppeteer = require('puppeteer-core');

const BASE = (process.env.GURKA_URL || 'http://127.0.0.1:5077').replace(/\/+$/, '');
const SHOTS = path.resolve(process.env.SHOTS || path.join(__dirname, 'shots'));
fs.mkdirSync(SHOTS, { recursive: true });

// webbläsare: BROWSER, annars Edge på standardplatsen i Windows
const BROWSER = process.env.BROWSER || [
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
].find(p => fs.existsSync(p));
if (!BROWSER) { console.error('Hittar ingen webbläsare. Sätt BROWSER till sökvägen till msedge/chrome.'); process.exit(2) }

const launch = (opts = {}) => puppeteer.launch({ executablePath: BROWSER, headless: true, ...opts });

// öppnar en sida i spelet; mot en Codespaces-länk klickas först varningssidan ("Codespaces Access Port", Continue) bort
async function open(page, url) {
  await page.goto(url, { waitUntil: 'networkidle2' });
  if ((await page.title()).includes('Codespaces')) {
    const btn = await page.$('button::-p-text(Continue)') || await page.$('button');
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle2' }), btn.click()]);
  }
}

module.exports = { BASE, SHOTS, BROWSER, launch, open };
