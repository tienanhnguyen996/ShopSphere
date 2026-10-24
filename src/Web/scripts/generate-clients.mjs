import { generate, HttpClient } from 'openapi-typescript-codegen';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
import { execSync } from 'node:child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, '../../..');
const webRoot = path.resolve(__dirname, '..');

const services = [
  {
    name: 'catalog',
    input: path.resolve(projectRoot, 'docs/specs/catalog-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/catalog'),
  },
  {
    name: 'cart',
    input: path.resolve(projectRoot, 'docs/specs/cart-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/cart'),
  },
  {
    name: 'inventory',
    input: path.resolve(projectRoot, 'docs/specs/inventory-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/inventory'),
  },
  {
    name: 'payment',
    input: path.resolve(projectRoot, 'docs/specs/payment-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/payment'),
  },
  {
    name: 'order',
    input: path.resolve(projectRoot, 'docs/specs/order-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/order'),
  },
  {
    name: 'shipping',
    input: path.resolve(projectRoot, 'docs/specs/shipping-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/shipping'),
  },
  {
    name: 'notification',
    input: path.resolve(projectRoot, 'docs/specs/notification-openapi.json'),
    output: path.resolve(webRoot, 'src/api/generated/notification'),
  },
];

async function main() {
  console.log('Generating OpenAPI TypeScript clients...');

  for (const service of services) {
    if (!fs.existsSync(service.input)) {
      throw new Error(`OpenAPI specification not found: ${service.input}`);
    }

    console.log(`Generating client for ${service.name}...`);
    console.log(`  Input:  ${service.input}`);
    console.log(`  Output: ${service.output}`);

    fs.rmSync(service.output, { recursive: true, force: true });

    await generate({
      input: service.input,
      output: service.output,
      httpClient: HttpClient.FETCH,
      useOptions: true,
    });

    console.log(`  ✓ Successfully generated client for ${service.name}\n`);
  }

  console.log('Formatting generated clients with Prettier...');
  execSync('pnpm exec prettier --write "src/api/generated/**/*.{ts,tsx}"', {
    cwd: webRoot,
    stdio: 'inherit',
  });

  console.log('All OpenAPI clients generated and formatted successfully.');
}

main().catch((err) => {
  console.error('Error generating OpenAPI clients:', err);
  process.exit(1);
});
