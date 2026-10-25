{
  description = "Otteretro: catálogo de sagas, bibliotecas, emuladores y ports, exportado a Steam";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      system = "x86_64-linux";
      pkgs = nixpkgs.legacyPackages.${system};
      lib = pkgs.lib;
      version = "0.0.0";

      # La interfaz: Next.js exportado como estático.
      web = pkgs.buildNpmPackage {
        pname = "otteretro-web";
        inherit version;
        src = lib.fileset.toSource {
          root = ./web;
          fileset = lib.fileset.difference ./web (lib.fileset.unions (map lib.fileset.maybeMissing [
            ./web/node_modules ./web/out ./web/.next ./web/e2e-results ./web/tsconfig.tsbuildinfo
          ]));
        };
        # Al cambiar web/package-lock.json: ver «Paquete de Nix» en AGENTS.md.
        npmDepsHash = "sha256-cJKiGoKROKms5O9b4IotqjKq+yCzu6fYj/ZVmD/qfzM=";
        env.NEXT_TELEMETRY_DISABLED = "1";
        installPhase = "cp -r out $out";
      };

      otteretro = pkgs.buildDotnetModule {
        pname = "otteretro";
        inherit version;
        src = lib.fileset.toSource {
          root = ./.;
          fileset = lib.fileset.unions [ ./src ./Directory.Build.props ./Otteretro.slnx ];
        };
        projectFile = "src/Otteretro/Otteretro.csproj";
        testProjectFile = "src/Otteretro.Tests/Otteretro.Tests.csproj";
        # Al cambiar un paquete NuGet: ver «Paquete de Nix» en AGENTS.md.
        nugetDeps = ./deps.json;
        dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;
        # El front lo compila buildNpmPackage; acá solo se copia.
        dotnetFlags = [ "-p:SkipWeb=true" ];
        doCheck = true;
        executables = [ "Otteretro" ];
        # Photino usa WebKitGTK; .NET necesita ICU.
        runtimeDeps = with pkgs; [ webkitgtk_4_1 gtk3 glib libnotify icu stdenv.cc.cc.lib ];
        # En Wayland con NVIDIA, WebKitGTK no dibuja: va por XWayland (#8). Se puede cambiar desde fuera.
        makeWrapperArgs = [
          "--set-default" "GDK_BACKEND" "x11"
          "--set-default" "WEBKIT_DISABLE_DMABUF_RENDERER" "1"
          # secret-tool guarda la llave maestra de las claves en el llavero (#9); xdg-open abre los enlaces.
          "--prefix" "PATH" ":" (lib.makeBinPath [ pkgs.libsecret pkgs.xdg-utils ])
        ];
        nativeBuildInputs = [ pkgs.copyDesktopItems ];
        desktopItems = [
          (pkgs.makeDesktopItem {
            name = "otteretro";
            desktopName = "Otteretro";
            comment = "Sagas, bibliotecas, emuladores y ports";
            exec = "Otteretro";
            icon = "otteretro";
            categories = [ "Game" ];
          })
        ];
        postInstall = ''
          cp -r ${web} $out/lib/otteretro/wwwroot
          install -Dm644 ${web}/nutria.png $out/share/icons/hicolor/128x128/apps/otteretro.png
        '';
        meta.mainProgram = "Otteretro";
      };
    in
    {
      packages.${system} = { default = otteretro; inherit web; };

      # Para PlayOS o cualquier NixOS: programs.otteretro.enable = true;
      nixosModules.default = { config, lib, ... }: {
        options.programs.otteretro.enable = lib.mkEnableOption "Otteretro";
        config = lib.mkIf config.programs.otteretro.enable {
          environment.systemPackages = [ self.packages.${system}.default ];
        };
      };

      # nix flake check: compila todo y corre dotnet test (doCheck).
      checks.${system}.default = otteretro;
    };
}
