namespace MacroGrid.Core.Plugins.Js;

public sealed partial class JsPlugin
{
    /// <summary>
    /// Builds the one object the script sees. The native delegates are captured in a closure and removed from the
    /// global scope, so the script cannot reach them (or replace what the wrappers call).
    /// </summary>
    private const string Bootstrap = """
        (function () {
          'use strict';
          const g = globalThis;
          const names = ['permissions', 'log', 'varSet', 'varGet', 'varRemove', 'varDescribe', 'registerAction',
            'settingsPage', 'settingsGet', 'status', 'hotkey', 'type', 'http', 'timer', 'cancel'];
          const n = {};
          for (const name of names) { n[name] = g['__' + name]; delete g['__' + name]; }

          const actions = Object.create(null);
          const timers = Object.create(null);
          let nextTimer = 1;
          const start = (fn, ms, repeat) => {
            if (typeof fn !== 'function') throw new Error('The timer needs a function.');
            const id = nextTimer++;
            timers[id] = { fn, repeat };
            n.timer(id, Math.floor(Number(ms)), repeat);
            return id;
          };
          const request = (method, url, body, options) =>
            JSON.parse(n.http(method, String(url), body === undefined ? '' : JSON.stringify(body), JSON.stringify((options && options.headers) || {})));

          const host = {
            permissions: Object.freeze(JSON.parse(n.permissions())),
            log: (message) => n.log(String(message)),
            variables: Object.freeze({
              set: (name, value) => n.varSet(String(name), value === undefined ? null : value),
              get: (name) => n.varGet(String(name)),
              remove: (name) => n.varRemove(String(name)),
              describe: (list) => n.varDescribe(JSON.stringify(list)),
            }),
            registerAction: (definition) => {
              if (!definition || typeof definition.run !== 'function') throw new Error('registerAction needs a run function.');
              const meta = Object.assign({}, definition);
              delete meta.run;
              n.registerAction(JSON.stringify(meta));
              actions[definition.type] = definition.run;
            },
            settings: Object.freeze({
              page: (fields) => n.settingsPage(JSON.stringify(fields)),
              get: () => JSON.parse(n.settingsGet()),
            }),
            status: (id, text, level) => n.status(String(id), String(text), String(level || 'Idle')),
            input: Object.freeze({ hotkey: (combo) => n.hotkey(String(combo)), type: (text) => n.type(String(text)) }),
            http: Object.freeze({
              get: (url, options) => request('GET', url, undefined, options),
              post: (url, body, options) => request('POST', url, body, options),
            }),
            every: (ms, fn) => start(fn, ms, true),
            after: (ms, fn) => start(fn, ms, false),
            cancel: (id) => { delete timers[id]; n.cancel(id); },
          };
          Object.defineProperty(g, 'host', { value: Object.freeze(host), writable: false, configurable: false });

          g.__runAction = function (type, contextJson, settingsJson) {
            const run = actions[type];
            if (!run) throw new Error('Unknown action ' + type);
            run(JSON.parse(contextJson), JSON.parse(settingsJson));
          };
          g.__fire = function (id) {
            const timer = timers[id];
            if (!timer) return;
            if (!timer.repeat) delete timers[id];
            timer.fn();
          };
        })();
        """;
}
