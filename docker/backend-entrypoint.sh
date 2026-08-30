#!/bin/sh
set -eu

if [ "$#" -gt 0 ]; then
  exec dotnet CoffeeShopApi.dll "$@"
fi

dotnet CoffeeShopApi.dll migrate
exec dotnet CoffeeShopApi.dll
