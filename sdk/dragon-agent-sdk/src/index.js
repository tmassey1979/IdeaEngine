function createAgent({ id, description, run }) {
  if (!id) {
    throw new Error("Agent id is required.");
  }

  if (typeof run !== "function") {
    throw new Error(`Agent "${id}" must provide a run function.`);
  }

  return {
    id,
    description: description || "",
    run
  };
}

function createLogger(scope) {
  return {
    info(message, data) {
      writeLog("info", scope, message, data);
    },
    warn(message, data) {
      writeLog("warn", scope, message, data);
    },
    error(message, data) {
      writeLog("error", scope, message, data);
    }
  };
}

function writeLog(level, scope, message, data) {
  const entry = {
    time: new Date().toISOString(),
    level,
    scope,
    message,
    ...(data === undefined ? {} : { data })
  };

  process.stderr.write(`${JSON.stringify(entry)}\n`);
}

module.exports = {
  createAgent,
  createLogger
};
