import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import ts from 'typescript';

export async function load(url, context, nextLoad) {
  if (url.endsWith('.ts') || url.endsWith('.tsx')) {
    const filePath = fileURLToPath(url);
    const source = fs.readFileSync(filePath, 'utf8');
    const { outputText } = ts.transpileModule(source, {
      compilerOptions: {
        module: ts.ModuleKind.ESNext,
        target: ts.ScriptTarget.ES2022,
        jsx: ts.JsxEmit.ReactJSX,
      },
      fileName: filePath,
    });
    return {
      format: 'module',
      shortCircuit: true,
      source: outputText,
    };
  }
  return nextLoad(url, context);
}
