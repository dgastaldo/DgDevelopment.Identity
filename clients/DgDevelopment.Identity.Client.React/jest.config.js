/** @type {import('@jest/types').Config.InitialOptions} */
module.exports = {
  testEnvironment: "node",
  // @swc/jest, not ts-jest: ts-jest's peer range caps below the TypeScript 7.x this package
  // pins, so it can't be installed here. swc parses/strips TS types on its own, independent of
  // the installed "typescript" package version.
  transform: {
    "^.+\\.ts$": "@swc/jest",
  },
  collectCoverageFrom: ["src/**/*.ts", "!src/**/*.test.ts"],
  coverageDirectory: "coverage",
  coverageReporters: ["lcov", "text"],
};
