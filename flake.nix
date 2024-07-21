{
  description = "Cookbook: my personal web-dingus.";

  inputs = {
    nixpkgs.url = "github:nixos/nixpkgs";
  };

  outputs = { self, nixpkgs }:

    let
      system = "x86_64-linux";

      pkgs = nixpkgs.legacyPackages.${system};

    in
    {
      # NOTE[gastove|2024-07-07] I don't think I need this.
      # packages.x86_64-linux.dotnet-sdk = pkgs.dotnet-sdk_8;

      devShells.${system}.default =
        pkgs.mkShell {
          buildInputs = [
            pkgs.dart-sass
            pkgs.dotnet-sdk_8
          ];

          DOTNET_ROOT = "${pkgs.dotnet-sdk_8}";
        };
    };

}
