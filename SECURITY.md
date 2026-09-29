# Seguridad de NetWatch NMS

## Secretos

- Nunca confirme `.env` ni `src/NetWatch.Agent/agent.json` en Git.
- Use claves diferentes para MySQL, root, usuarios, JWT y agentes.
- `Jwt__Key` debe tener al menos 32 bytes aleatorios; el script genera 48 bytes.
- Rote la clave del agente si un equipo o archivo de configuración se compromete.
- Los valores de desarrollo incluidos en `appsettings.Development.json` son solo para ejecución local aislada.

## Contraseñas y sesiones

- Las contraseñas se guardan con `PasswordHasher<TUser>`; nunca en texto plano.
- La API solo autentica usuarios activos.
- Los JWT caducan; cambiar `Jwt__Key` invalida inmediatamente todos los tokens existentes.
- En producción, proteja panel y API con HTTPS y no registre cabeceras `Authorization` o `X-Agent-Key`.

## Red

- MySQL permanece solo en la red Docker.
- En laboratorio, limite 8080/8081 a `192.168.56.0/24`.
- En producción, exponga únicamente 80/443 mediante reverse proxy; filtre el origen de los agentes si la topología lo permite.
- `verifyTls` debe ser `true`; no acepte certificados inválidos.

## Datos e historial

- Usuarios y dispositivos se desactivan lógicamente.
- La API no expone operaciones para modificar o borrar métricas, eventos o estados históricos.
- Proteja y cifre los respaldos; pruebe la restauración.

## Antes de publicar

1. Elimine cuentas de prueba que no sean necesarias.
2. Cambie todos los secretos y configure DNS/certificado.
3. Aplique actualizaciones del sistema, Docker e imágenes base.
4. Ejecute `scripts/Test-NetWatch.ps1` y el plan manual.
5. Revise `git status` y el historial para confirmar que no hubo secretos expuestos.
6. Configure monitoreo de logs, respaldos y procedimiento de respuesta a incidentes.

Para reportar una vulnerabilidad en un entorno real, use un canal privado del responsable del sistema; no publique claves, tokens, IP internas ni datos personales en un issue público.
