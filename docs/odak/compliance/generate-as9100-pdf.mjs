import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { mdToPdf } from 'md-to-pdf';

const __dirname = dirname(fileURLToPath(import.meta.url));
const input = join(__dirname, 'AS9100_MUSTERI_OZET.md');
const output = join(__dirname, 'AS9100_MUSTERI_OZET.pdf');
const stylesheet = join(__dirname, 'as9100-pdf.css');

const edgePath =
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';

const markdown = readFileSync(input, 'utf8');

const pdf = await mdToPdf(
  { content: markdown },
  {
    dest: output,
    basedir: __dirname,
    stylesheet: [stylesheet],
    pdf_options: {
      format: 'A4',
      printBackground: true,
      margin: { top: '18mm', right: '16mm', bottom: '20mm', left: '16mm' },
    },
    launch_options: {
      executablePath: edgePath,
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox'],
    },
  },
);

if (!pdf) {
  console.error('PDF oluşturulamadı.');
  process.exit(1);
}

console.log(`PDF hazır: ${resolve(output)}`);
