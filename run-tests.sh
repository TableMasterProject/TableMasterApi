#!/bin/bash

# Script pour exécuter les tests unitaires de TableMasterApi
# Utilisez ce script pour lancer rapidement les tests

set -e

echo "========================================="
echo "🧪 Tests Unitaires TableMasterApi"
echo "========================================="
echo ""

cd "$(dirname "$0")/TableMasterApi"

echo "📦 Restauration des dépendances..."
dotnet restore TableMasterApi.Tests/TableMasterApi.Tests.csproj > /dev/null 2>&1

echo "🔨 Compilation..."
dotnet build TableMasterApi.Tests/TableMasterApi.Tests.csproj -c Release > /dev/null 2>&1

echo ""
echo "🚀 Exécution des tests..."
echo "========================================="
dotnet test TableMasterApi.Tests/TableMasterApi.Tests.csproj \
    -c Release \
    --logger "console;verbosity=normal" \
    --no-build

echo ""
echo "========================================="
echo "✅ Tests terminés !"
echo "========================================="
