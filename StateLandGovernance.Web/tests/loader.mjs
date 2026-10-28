import fs from "node:fs";
import path from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import ts from "typescript";

const rootDir = process.cwd();
const srcDir = path.join(rootDir, "src");

function findFileCandidate(candidateBase) {
  const candidates = [
    candidateBase,
    candidateBase + ".ts",
    candidateBase + ".tsx",
    candidateBase + ".js",
    path.join(candidateBase, "index.ts"),
    path.join(candidateBase, "index.tsx"),
  ];

  for (const c of candidates) {
    if (fs.existsSync(c) && !fs.statSync(c).isDirectory()) {
      return pathToFileURL(c).href;
    }
  }
  return null;
}

export async function resolve(specifier, context, nextResolve) {
  if (specifier === "next/link") return nextResolve("next/link.js", context);
  if (specifier === "next/server") return nextResolve("next/server.js", context);
  if (specifier === "next/navigation") return nextResolve("next/navigation.js", context);
  if (specifier.startsWith("@/")) {
    const subpath = specifier.slice(2);
    const candidateBase = path.join(srcDir, subpath);
    const resolved = findFileCandidate(candidateBase);
    if (resolved) {
      return nextResolve(resolved, context);
    }
  } else if (specifier.startsWith("./") || specifier.startsWith("../")) {
    const parentDir = context.parentURL ? path.dirname(fileURLToPath(context.parentURL)) : rootDir;
    const candidateBase = path.resolve(parentDir, specifier);
    const resolved = findFileCandidate(candidateBase);
    if (resolved) {
      return nextResolve(resolved, context);
    }
  }

  return nextResolve(specifier, context);
}

export async function load(url, context, nextLoad) {
  if (url.endsWith(".json")) {
    return {format:"module",shortCircuit:true,source:`export default ${JSON.stringify(JSON.parse(fs.readFileSync(fileURLToPath(url),"utf8")))};`};
  }
  if (url.endsWith(".css")) {
    return {
      format: "module",
      shortCircuit: true,
      source: "export default new Proxy({}, { get: (target, prop) => String(prop) });",
    };
  }

  if (url.endsWith(".tsx")) {
    const filePath = fileURLToPath(url);
    const sourceCode = fs.readFileSync(filePath, "utf-8");
    const transpiled = ts.transpileModule(sourceCode, {
      compilerOptions: {
        module: ts.ModuleKind.ESNext,
        target: ts.ScriptTarget.ES2022,
        jsx: ts.JsxEmit.ReactJSX,
        esModuleInterop: true,
      },
    });

    return {
      format: "module",
      shortCircuit: true,
      source: transpiled.outputText,
    };
  }

  return nextLoad(url, context);
}
