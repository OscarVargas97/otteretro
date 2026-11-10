# Lo propio de Otteretro: el front vive en web/ (el perfil node espera package.json en la raíz).
local_rapidos() {
  echo "== web (revisar)"
  [ -d web/node_modules ] || npm --prefix web ci
  npm --prefix web run -s revisar
}
local_ci() { :; }
