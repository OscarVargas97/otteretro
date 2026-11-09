import { test } from "node:test";
import assert from "node:assert/strict";
import { nestedForkIds } from "./forks.ts";

test("un fork se anida solo si su original está en la lista", () => {
  const forkOf = (id: string) => ({ a: "orig", b: "orig", c: "ausente" })[id as "a"];
  assert.deepEqual([...nestedForkIds([{ id: "orig" }, { id: "a" }, { id: "b" }, { id: "c" }], forkOf)], ["a", "b"]);
});
