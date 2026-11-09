import assert from "node:assert/strict";
import {
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  readdirSync,
  rmSync,
  statSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath } from "node:url";
import {
  auditSource,
  DEFAULT_ROOT,
  inspectTabBodies,
  listSourceFiles,
} from "./audit-frontend-conventions.mjs";

const repositoryRoot = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../../..",
);
const skillRoot = resolve(
  repositoryRoot,
  ".agents/skills/angular-frontend-standards",
);

function fixture(path) {
  return readFileSync(resolve(repositoryRoot, path), "utf8");
}

function markdownFiles(directory) {
  return readdirSync(directory).flatMap((name) => {
    const path = resolve(directory, name);
    return statSync(path).isDirectory()
      ? markdownFiles(path)
      : path.endsWith(".md")
        ? [path]
        : [];
  });
}

test("classifies inline and component-only tab bodies", () => {
  const source = `
    <mat-tab-group>
      <mat-tab label="Inline"><div>Business content</div></mat-tab>
      <mat-tab label="Component"><ng-template matTabContent><app-orders-tab /></ng-template></mat-tab>
    </mat-tab-group>`;

  assert.deepEqual(
    inspectTabBodies(source).map((tab) => tab.component),
    [false, true],
  );
  assert.equal(
    auditSource("src/app/features/example/example.html", source).filter(
      (finding) => finding.rule === "TAB_COMPONENT_BOUNDARY",
    ).length,
    1,
  );
});

test("supports the legacy NG-ZORRO tab pattern only as an optional audit target", () => {
  const source = `<nz-tab nzTitle="Component"><ng-template nz-tab><app-orders-tab /></ng-template></nz-tab>`;
  assert.deepEqual(
    inspectTabBodies(source).map((tab) => tab.component),
    [true],
  );
});

test("rejects Angular Material imports and widgets after migration", () => {
  const typescript = `import { MatButtonModule } from '@angular/material/button';`;
  const html = `<button mat-button>Save</button><mat-icon>save</mat-icon>`;
  assert.ok(
    auditSource("src/app/example.component.ts", typescript).some(
      (finding) => finding.rule === "MATERIAL_IMPORT_FORBIDDEN",
    ),
  );
  assert.ok(
    auditSource("src/app/example.component.html", html).some(
      (finding) => finding.rule === "MATERIAL_WIDGET_FORBIDDEN",
    ),
  );
});

test("flags a possible hand-drawn table for review", () => {
  const source = `
    @for (quote of quotes(); track quote.id) {
      <a class="flex justify-between"><span>{{ quote.number }}</span><span>{{ quote.total }}</span></a>
    }`;

  assert.ok(
    auditSource("src/app/features/example/quotes-tab.html", source).some(
      (finding) => finding.rule === "POSSIBLE_HAND_DRAWN_TABLE",
    ),
  );
});

test("fails route-page tables without treating feature child components as pages", () => {
  const source = `<table><tr><td>Job</td></tr></table>`;
  const routePage = auditSource(
    "src/app/features/omc/pages/jobs/jobs.component.html",
    source,
  );
  const childTable = auditSource(
    "src/app/features/omc/pages/jobs/job-table.component.html",
    source,
  );

  assert.equal(
    routePage.find((finding) => finding.rule === "PAGE_INLINE_DATA_TABLE")
      ?.enforcement,
    "MUST-AUTO",
  );
  assert.equal(
    childTable.some((finding) => finding.rule === "PAGE_INLINE_DATA_TABLE"),
    false,
  );
});

test("fails clear page-owned async orchestration while retaining borderline cases for review", () => {
  const state = `loading = signal(false); error = signal(null);`;
  const subscription = `source.subscribe();`;
  const page = "src/app/features/omc/pages/jobs/jobs.component.ts";
  const clearViolation = auditSource(page, `${state}${subscription.repeat(5)}`);
  const borderline = auditSource(page, `${state}${subscription.repeat(3)}`);

  assert.equal(
    clearViolation.find(
      (finding) => finding.rule === "PAGE_ASYNC_ORCHESTRATION",
    )?.enforcement,
    "MUST-AUTO",
  );
  assert.equal(
    borderline.find((finding) => finding.rule === "PAGE_ASYNC_ORCHESTRATION")
      ?.enforcement,
    "MUST-REVIEW",
  );
});

test("strict CLI exits nonzero for an automatic boundary violation", (context) => {
  const fixtureRoot = mkdtempSync(join(tmpdir(), "aubot-frontend-audit-"));
  try {
    const pageDirectory = join(fixtureRoot, "pages", "jobs");
    mkdirSync(pageDirectory, { recursive: true });
    writeFileSync(
      join(pageDirectory, "jobs.component.html"),
      "<table></table>",
    );
    const result = spawnSync(
      process.execPath,
      [
        resolve(skillRoot, "scripts/audit-frontend-conventions.mjs"),
        fixtureRoot,
        "--strict",
      ],
      { encoding: "utf8" },
    );
    if (result.error?.code === "EPERM") {
      context.skip(
        "This sandbox blocks child processes; CI exercises the strict CLI.",
      );
      return;
    }
    assert.equal(
      result.status,
      1,
      result.stderr || result.stdout || String(result.error),
    );
    assert.match(result.stdout, /MUST-AUTO PAGE_INLINE_DATA_TABLE/);
  } finally {
    assert.equal(resolve(dirname(fixtureRoot)), resolve(tmpdir()));
    rmSync(fixtureRoot, { recursive: true, force: true });
  }
});

test("validates the skill frontmatter and every routed reference", () => {
  const skill = readFileSync(resolve(skillRoot, "SKILL.md"), "utf8");
  const frontmatter = skill.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  assert.ok(frontmatter, "SKILL.md must contain YAML frontmatter");
  assert.match(frontmatter[1], /^name: angular-frontend-standards$/m);
  assert.match(frontmatter[1], /^description: \S.+$/m);

  const references = [...skill.matchAll(/\]\(references\/([^)]+\.md)\)/g)].map(
    (match) => match[1],
  );
  assert.equal(
    references.length,
    9,
    "router must link the repository profile, seven topics, and shared form feedback reference",
  );
  for (const reference of references) {
    assert.ok(
      existsSync(resolve(skillRoot, "references", reference)),
      `${reference} must exist`,
    );
  }
});

test("uses the ASRS Angular application as the convention-audit default", () => {
  const appPath = resolve(repositoryRoot, DEFAULT_ROOT);
  assert.ok(existsSync(appPath));
  assert.ok(listSourceFiles(appPath).length > 0);
});

test("workflow skills no longer suppress required lint or test gates", () => {
  const workflows = [
    ".agents/skills/create-angular-feature/SKILL.md",
    ".agents/skills/refactor-angular-code/SKILL.md",
    ".agents/skills/review-angular-code/SKILL.md",
  ];
  for (const workflow of workflows) {
    const source = fixture(workflow);
    assert.doesNotMatch(source, /Do not run tests, lint/i);
    assert.match(source, /angular-frontend-standards/);
  }
});

test("all local markdown references in the frontend skill resolve", () => {
  for (const file of markdownFiles(skillRoot)) {
    const source = readFileSync(file, "utf8");
    for (const match of source.matchAll(/\]\(([^)#]+\.md)(?:#[^)]+)?\)/g)) {
      assert.ok(
        existsSync(resolve(dirname(file), match[1])),
        `${relative(repositoryRoot, file)} links to missing ${match[1]}`,
      );
    }
  }
});
