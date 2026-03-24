const { defineConfig } = require("vitest/config");

module.exports = defineConfig({
  test: {
    environment: "jsdom",
    include: ["tests/ui-unit/**/*.test.mjs"],
  },
});
