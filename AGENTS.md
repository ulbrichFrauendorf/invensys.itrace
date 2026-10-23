# Project instructions

- Do not create README files for individual components. Put usage examples in demo pages or existing shared documentation.
- Backend C#: use exactly one blank line between consecutive statements, including local declarations, assignments, calls, returns, and control-flow statements. Keep compound constructs such as if/else and try/catch together. Do not insert blank lines between every object-initializer entry, parameter, or using directive. Leave generated EF designer/snapshot code to its generator.
- Frontend: format Angular/TypeScript, HTML, SCSS, JavaScript, and JSON with the ClientApp Prettier configuration. Do not apply the backend statement-spacing rule to frontend code. Run npm run format and npm run format:check from src/Web/ClientApp.
- After regenerating frontend API clients, format their generated output with Prettier rather than hand-editing generated logic.
