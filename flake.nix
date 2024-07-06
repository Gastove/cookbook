{
  description = "Cookbook: my personal web-dingus.";

  inputs = { nixpkgs.url = "github:nixos/nixpkgs";};

  outputs = { self, nixpkgs }:

    let pkgs =
          nixpkgs.legacyPackages.x86_64-linux;

    in {

      packages.x86_64-linux.dotnet-sdk = pkgs.dotnet-sdk_8;

      devShell.x86_64-linux =
        pkgs.mkShell {
          buildInputs = [
            self.packages.x86_64-linux.dotnet-sdk
          ];

          DOTNET_ROOT="${pkgs.dotnet-sdk_8}";          
        };      
    };

}
