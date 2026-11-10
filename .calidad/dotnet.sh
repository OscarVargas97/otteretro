# ia-qa, perfil dotnet: dotnet format (verify), build y test. Se detecta por *.sln / *.slnx / *.csproj.
# Con varias soluciones en la raíz, `dotnet` pide una: usa la primera (shortcut: monorepos con varias, fijar DOTNET_SLN en .calidad/local.sh).
# Sin solución no se pasa argumento: `dotnet` toma el .csproj de la carpeta (un "" lo hace fallar).
dotnet_objetivo() { echo "${DOTNET_SLN:-$(ls ./*.sln ./*.slnx 2>/dev/null | head -n1)}"; }
dotnet_cmd() { c=$1; shift; o=$(dotnet_objetivo); if [ -n "$o" ]; then dotnet "$c" "$o" "$@"; else dotnet "$c" "$@"; fi; }

dotnet_rapidos() {
  echo "== dotnet format"
  dotnet_cmd format --verify-no-changes
}

dotnet_ci() {
  dotnet_rapidos
  echo "== dotnet build"
  dotnet_cmd build -warnaserror --nologo -v q
  # Cambio respecto del catálogo: en GitHub Actions los tests los corre `nix flake check` (necesitan 7z, chdman… del paquete de Nix; en ubuntu pelado fallan 109).
  if [ -n "$GITHUB_ACTIONS" ]; then echo "== dotnet test: lo cubre nix flake check"; return 0; fi
  echo "== dotnet test"
  dotnet_cmd test --no-build --nologo -v q
}
