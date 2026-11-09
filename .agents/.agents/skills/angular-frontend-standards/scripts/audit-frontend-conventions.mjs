#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { dirname, extname, relative, resolve, sep } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const AUTO = "MUST-AUTO";
const REVIEW = "MUST-REVIEW";
const HTML_EXTENSIONS = new Set([".html"]);
const TS_EXTENSIONS = new Set([".ts"]);
export const DEFAULT_ROOT = "src/Aubot.WCS.WebApp/src/app";

function lineNumber(source, index) {
  return source.slice(0, index).split("\n").length;
}

function addFinding(findings, file, source, index, rule, enforcement, message) {
  findings.push({
    file,
    line: lineNumber(source, Math.max(0, index)),
    rule,
    enforcement,
    message,
  });
}

function componentTagCount(body) {
  return [...body.matchAll(/<app-[a-z0-9-]+\b/gi)].length;
}

function hasOnlyComponentMarkup(body) {
  const tags = [...body.matchAll(/<\/?([a-z][a-z0-9-]*)\b[^>]*>/gi)].map(
    (match) => match[1].toLowerCase(),
  );
  return (
    componentTagCount(body) === 1 &&
    tags.every((tag) => tag === "ng-template" || tag.startsWith("app-"))
  );
}

function isRoutePageComponent(file) {
  const parts = file.split(/[\\/]/);
  const pageIndex = parts.lastIndexOf("pages");
  if (pageIndex < 0) return false;

  const remaining = parts.slice(pageIndex + 1);
  if (remaining.length === 1)
    return /\.component\.(?:html|ts)$/.test(remaining[0]);
  if (remaining.length !== 2) return false;
  return (
    remaining[1] === `${remaining[0]}.component.html` ||
    remaining[1] === `${remaining[0]}.component.ts`
  );
}

export function inspectTabBodies(source) {
  const results = [];
  const tabPattern = /<(mat-tab|nz-tab)\b[\s\S]*?<\/\1>/gi;
  for (const match of source.matchAll(tabPattern)) {
    const block = match[0];
    const openEnd = block.indexOf(">");
    const closeStart = block
      .toLowerCase()
      .lastIndexOf(`</${match[1].toLowerCase()}>`);
    const body = block.slice(openEnd + 1, closeStart);
    results.push({
      index: match.index ?? 0,
      component: hasOnlyComponentMarkup(body),
    });
  }
  return results;
}

function auditHtml(file, source) {
  const findings = [];
  const isPage = isRoutePageComponent(file);

  for (const match of source.matchAll(/<mat-[a-z0-9-]+\b|\bmatInput\b/gi)) {
    addFinding(
      findings,
      file,
      source,
      match.index ?? 0,
      "MATERIAL_WIDGET_FORBIDDEN",
      AUTO,
      "Angular Material widgets were removed; use the NG-ZORRO equivalent.",
    );
  }

  for (const tab of inspectTabBodies(source)) {
    if (!tab.component) {
      addFinding(
        findings,
        file,
        source,
        tab.index,
        "TAB_COMPONENT_BOUNDARY",
        AUTO,
        "Inline tab body: every tab must render exactly one feature-local component.",
      );
    }
  }

  const tablePattern = /<(?:table|nz-table)\b/i;
  if (isPage && tablePattern.test(source)) {
    addFinding(
      findings,
      file,
      source,
      source.search(tablePattern),
      "PAGE_INLINE_DATA_TABLE",
      AUTO,
      "Route page contains a data table; extract it into a feature-local component.",
    );
  }

  const pageOverlay = source.search(
    /<(?:mat-dialog|mat-drawer|nz-modal|nz-drawer)\b/i,
  );
  if (isPage && pageOverlay >= 0) {
    addFinding(
      findings,
      file,
      source,
      pageOverlay,
      "PAGE_INLINE_OVERLAY",
      AUTO,
      "Route page contains an inline modal/drawer body instead of a feature-local surface component.",
    );
  }

  for (const match of source.matchAll(/@for\s*\([^\n]+\)\s*\{/g)) {
    const index = match.index ?? 0;
    const window = source.slice(index, index + 1800);
    const possibleColumns =
      /float-right|grid-cols-\[|grid-template-columns|justify-between/i.test(
        window,
      );
    const valueCount = [...window.matchAll(/{{/g)].length;
    if (
      possibleColumns &&
      valueCount >= 2 &&
      !/<(?:table|nz-table)\b/i.test(window)
    ) {
      addFinding(
        findings,
        file,
        source,
        index,
        "POSSIBLE_HAND_DRAWN_TABLE",
        REVIEW,
        "Repeated multi-field layout may have row-column semantics; confirm whether the installed table pattern is appropriate.",
      );
    }
  }

  const rawControlPatterns = [
    {
      tag: "button",
      pattern: /<button\b(?![^>]*\bnz-button\b)[^>]*>/gi,
    },
    {
      tag: "input",
      pattern: /<input\b(?![^>]*\bnz-input\b)[^>]*>/gi,
    },
    { tag: "select", pattern: /<select\b[^>]*>/gi },
    {
      tag: "textarea",
      pattern: /<textarea\b(?![^>]*\bnz-input\b)[^>]*>/gi,
    },
  ];
  for (const { tag, pattern } of rawControlPatterns) {
    for (const match of source.matchAll(pattern)) {
      addFinding(
        findings,
        file,
        source,
        match.index ?? 0,
        "RAW_LIBRARY_EQUIVALENT_CONTROL",
        REVIEW,
        `Raw ${tag} control: verify that NG-ZORRO, Angular CDK, or a shared component does not provide the required capability.`,
      );
    }
  }

  for (const match of source.matchAll(/\sstyle="[^"]*"/gi)) {
    addFinding(
      findings,
      file,
      source,
      match.index ?? 0,
      "INLINE_STYLE",
      REVIEW,
      "Inline style: prefer a token, Tailwind utility, component input, or scoped class.",
    );
  }

  for (const match of source.matchAll(/\.(?:sort|filter|map|reduce)\s*\(/g)) {
    addFinding(
      findings,
      file,
      source,
      match.index ?? 0,
      "TEMPLATE_DATA_TRANSFORMATION",
      AUTO,
      "Template transforms data; prepare it in computed state, a selector, facade view model, or pure pipe.",
    );
  }

  const lines = source.split("\n").length;
  if (lines > 250) {
    addFinding(
      findings,
      file,
      source,
      0,
      "TEMPLATE_RESPONSIBILITY_REVIEW",
      REVIEW,
      `Template has ${lines} lines; inventory its independent surfaces and record why it remains cohesive.`,
    );
  }

  return findings;
}

function featureName(file) {
  const parts = file.split(/[\\/]/);
  const featureIndex = parts.lastIndexOf("features");
  return featureIndex >= 0 ? parts[featureIndex + 1] : null;
}

function auditTypeScript(file, source, absoluteFile) {
  const findings = [];
  const currentFeature = featureName(file);

  for (const match of source.matchAll(/from\s+['"]@angular\/material(?:\/[^'"]*)?['"]/g)) {
    addFinding(
      findings,
      file,
      source,
      match.index ?? 0,
      "MATERIAL_IMPORT_FORBIDDEN",
      AUTO,
      "Angular Material was removed; use NG-ZORRO and keep Angular CDK for interaction infrastructure.",
    );
  }

  if (currentFeature) {
    for (const match of source.matchAll(/from\s+['"]([^'"]+)['"]/g)) {
      const specifier = match[1];
      if (!specifier.startsWith(".")) continue;
      const target = resolve(dirname(absoluteFile), specifier);
      const targetFeature = featureName(target);
      if (
        targetFeature &&
        targetFeature !== currentFeature &&
        !target.split(/[\\/]/).includes("public")
      ) {
        addFinding(
          findings,
          file,
          source,
          match.index ?? 0,
          "CROSS_FEATURE_IMPORT",
          REVIEW,
          `Relative import crosses from ${currentFeature} to ${targetFeature} without an explicit public feature contract.`,
        );
      }
    }
  }

  const isPage = isRoutePageComponent(file);
  if (isPage) {
    const subjects = [...source.matchAll(/new\s+Subject\s*</g)].length;
    const requestIds = [...source.matchAll(/requestId|RequestId/g)].length;
    const subscriptions = [...source.matchAll(/\.subscribe\s*\(/g)].length;
    const asyncStates = [
      ...source.matchAll(/(?:loading|error)\w*\s*=\s*signal/gi),
    ].length;
    if (
      (subjects >= 2 && requestIds >= 1) ||
      (subscriptions >= 3 && asyncStates >= 2)
    ) {
      const definiteFacadeBoundary = subscriptions >= 5 && asyncStates >= 2;
      addFinding(
        findings,
        file,
        source,
        0,
        "PAGE_ASYNC_ORCHESTRATION",
        definiteFacadeBoundary ? AUTO : REVIEW,
        `Page owns ${subjects} Subject(s), ${subscriptions} subscription(s), ${requestIds} request-id reference(s), and ${asyncStates} async state signal(s); move independent async workflows into a feature facade.`,
      );
    }
  }

  const lines = source.split("\n").length;
  if (!file.endsWith(".spec.ts") && lines > 300) {
    addFinding(
      findings,
      file,
      source,
      0,
      "TYPESCRIPT_RESPONSIBILITY_REVIEW",
      REVIEW,
      `TypeScript file has ${lines} lines; inventory mixed responsibilities and record why it remains cohesive.`,
    );
  }

  return findings;
}

export function auditSource(file, source, absoluteFile = resolve(file)) {
  const extension = extname(file).toLowerCase();
  if (HTML_EXTENSIONS.has(extension)) return auditHtml(file, source);
  if (TS_EXTENSIONS.has(extension))
    return auditTypeScript(file, source, absoluteFile);
  return [];
}

export function listSourceFiles(directory) {
  const files = [];
  for (const name of readdirSync(directory)) {
    const path = resolve(directory, name);
    const metadata = statSync(path);
    if (metadata.isDirectory()) files.push(...listSourceFiles(path));
    else if (
      HTML_EXTENSIONS.has(extname(path)) ||
      TS_EXTENSIONS.has(extname(path))
    )
      files.push(path);
  }
  return files;
}

function changedFiles(repositoryRoot, base) {
  const diffArgs = base
    ? ["diff", "--name-only", "--diff-filter=ACMR", `${base}...HEAD`, "--"]
    : ["diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--"];
  const tracked = execFileSync("git", diffArgs, {
    cwd: repositoryRoot,
    encoding: "utf8",
  });
  const workingTree = base
    ? execFileSync(
        "git",
        ["diff", "--name-only", "--diff-filter=ACMR", "HEAD", "--"],
        {
          cwd: repositoryRoot,
          encoding: "utf8",
        },
      )
    : "";
  const untracked = execFileSync(
    "git",
    ["ls-files", "--others", "--exclude-standard"],
    {
      cwd: repositoryRoot,
      encoding: "utf8",
    },
  );
  return new Set(
    `${tracked}\n${workingTree}\n${untracked}`
      .split(/\r?\n/)
      .filter(Boolean)
      .map((file) => resolve(repositoryRoot, file)),
  );
}

function parseArguments(argv) {
  const options = {
    root: null,
    json: false,
    strict: false,
    changed: false,
    base: null,
  };
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index];
    if (value === "--json") options.json = true;
    else if (value === "--strict") options.strict = true;
    else if (value === "--changed") options.changed = true;
    else if (value === "--base") options.base = argv[++index] ?? null;
    else if (!options.root) options.root = value;
    else throw new Error(`Unknown argument: ${value}`);
  }
  return options;
}

function run() {
  const options = parseArguments(process.argv.slice(2));
  const repositoryRoot = resolve(
    dirname(fileURLToPath(import.meta.url)),
    "../../../..",
  );
  const root = resolve(repositoryRoot, options.root ?? DEFAULT_ROOT);
  const changed = options.changed
    ? changedFiles(repositoryRoot, options.base)
    : null;
  const files = listSourceFiles(root).filter(
    (file) => !changed || changed.has(file),
  );
  const findings = files
    .flatMap((absoluteFile) => {
      const file = relative(repositoryRoot, absoluteFile).split(sep).join("/");
      return auditSource(
        file,
        readFileSync(absoluteFile, "utf8"),
        absoluteFile,
      );
    })
    .sort(
      (left, right) =>
        left.file.localeCompare(right.file) || left.line - right.line,
    );

  if (options.json) {
    process.stdout.write(
      `${JSON.stringify({ root, files: files.length, findings }, null, 2)}\n`,
    );
  } else {
    for (const finding of findings) {
      process.stdout.write(
        `${finding.enforcement} ${finding.rule} ${finding.file}:${finding.line} ${finding.message}\n`,
      );
    }
    const automatic = findings.filter(
      (finding) => finding.enforcement === AUTO,
    ).length;
    const review = findings.length - automatic;
    process.stdout.write(
      `Audited ${files.length} file(s): ${automatic} automatic finding(s), ${review} review finding(s).\n`,
    );
  }

  if (
    options.strict &&
    findings.some((finding) => finding.enforcement === AUTO)
  ) {
    process.exitCode = 1;
  }
}

const entryPath = process.argv[1]
  ? pathToFileURL(resolve(process.argv[1])).href
  : "";
if (import.meta.url === entryPath) run();
