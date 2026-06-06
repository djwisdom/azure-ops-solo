using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MyCrownJewelApp.Pfpad;

internal sealed class NewProjectDialog : Form
{
    // ── Template model ────────────────────────────────────────────────────────
    private sealed record ProjectTemplate(
        string Name,
        string ShortName,
        string Language,        // "C#", "C", "C++"
        string Tags,
        string? DotnetShortName,                              // non-null → use dotnet new
        IReadOnlyList<(string RelPath, string Content)> Files // for C/C++
    );

    // ── Controls ──────────────────────────────────────────────────────────────
    private readonly Form1 _mainForm;
    private ComboBox _langFilter = null!;
    private ListView _templateList = null!;
    private TextBox _nameTextBox = null!;
    private TextBox _locationTextBox = null!;
    private Button _browseButton = null!;
    // dotnet-specific
    private CheckBox _solutionCheckBox = null!;
    private Label _frameworkLabel = null!;
    private ComboBox _frameworkCombo = null!;
    // C/C++-specific
    private CheckBox _gitCheckBox = null!;
    private Label _standardLabel = null!;
    private ComboBox _standardCombo = null!;

    private Button _createButton = null!;
    private Button _cancelButton = null!;
    private Label _statusLabel = null!;
    private bool _creating;

    private List<ProjectTemplate> _allTemplates = new();

    public NewProjectDialog(Form1 mainForm)
    {
        _mainForm = mainForm;
        Text = "New Project";
        Size = new Size(700, 560);
        MinimumSize = new Size(700, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;

        InitializeForm();
        ApplyTheme();
        BuildBuiltInTemplates();
        LoadDotnetTemplates();
        PopulateTemplateList("All");
        LoadDotnetFrameworks();
    }

    private void InitializeForm()
    {
        int y = 12;

        // ── Language filter row ───────────────────────────────────────────────
        var filterLabel = new Label { Text = "&Language:", Location = new Point(12, y + 3), AutoSize = true };
        _langFilter = new ComboBox
        {
            Location = new Point(80, y),
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _langFilter.Items.AddRange(new object[] { "All", "C#", "C", "C++", "Bicep", "Terraform" });
        _langFilter.SelectedIndex = 0;
        _langFilter.SelectedIndexChanged += (s, e) => PopulateTemplateList(_langFilter.SelectedItem as string ?? "All");
        y += 34;

        // ── Template list ─────────────────────────────────────────────────────
        var templateLabel = new Label { Text = "&Template:", Location = new Point(12, y), AutoSize = true };
        y += 16;
        _templateList = new ListView
        {
            Location = new Point(12, y),
            Size = new Size(662, 180),
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Clickable
        };
        _templateList.Columns.Add("Short Name", 140);
        _templateList.Columns.Add("Template Name", 280);
        _templateList.Columns.Add("Language", 70);
        _templateList.Columns.Add("Tags", 140);
        _templateList.SelectedIndexChanged += TemplateList_SelectedIndexChanged;
        y += 188;

        // ── Project name ──────────────────────────────────────────────────────
        var nameLabel = new Label { Text = "Project &name:", Location = new Point(12, y), AutoSize = true };
        y += 16;
        _nameTextBox = new TextBox { Text = "MyApp", Location = new Point(12, y), Width = 330 };
        y += 32;

        // ── Location ──────────────────────────────────────────────────────────
        var locationLabel = new Label { Text = "&Location:", Location = new Point(12, y), AutoSize = true };
        y += 16;
        _locationTextBox = new TextBox { Location = new Point(12, y), Width = 560 };
        _browseButton = new Button { Text = "&Browse...", Location = new Point(580, y - 1), Width = 94, Height = 26 };
        _browseButton.Click += BrowseButton_Click;

        string? wsRoot = _mainForm.WorkspaceRoot;
        if (!string.IsNullOrEmpty(wsRoot)) { var p = Directory.GetParent(wsRoot); if (p != null) _locationTextBox.Text = p.FullName; }
        if (string.IsNullOrEmpty(_locationTextBox.Text))
            _locationTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        y += 32;

        // ── dotnet-specific options ────────────────────────────────────────────
        _solutionCheckBox = new CheckBox { Text = "Create &solution file (.sln)", Location = new Point(12, y), AutoSize = true, Checked = true };
        _frameworkLabel = new Label { Text = "&Framework:", Location = new Point(340, y + 3), AutoSize = true };
        _frameworkCombo = new ComboBox { Location = new Point(420, y), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };

        // ── C/C++ specific options ────────────────────────────────────────────
        _gitCheckBox = new CheckBox { Text = "Initialize &Git repository", Location = new Point(12, y), AutoSize = true, Checked = true, Visible = false };
        _standardLabel = new Label { Text = "&Standard:", Location = new Point(340, y + 3), AutoSize = true, Visible = false };
        _standardCombo = new ComboBox { Location = new Point(420, y), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
        y += 34;

        // ── Status label ──────────────────────────────────────────────────────
        _statusLabel = new Label { Text = "", Location = new Point(12, y), Size = new Size(662, 30), ForeColor = Color.Gray };
        y += 34;

        // ── Buttons ───────────────────────────────────────────────────────────
        _createButton = new Button { Text = "&Create", Location = new Point(510, y), Width = 80, Height = 28 };
        _createButton.Click += CreateButton_Click;
        _cancelButton = new Button { Text = "Cancel", Location = new Point(600, y), Width = 80, Height = 28, DialogResult = DialogResult.Cancel };

        Controls.AddRange(new Control[] {
            filterLabel, _langFilter,
            templateLabel, _templateList,
            nameLabel, _nameTextBox,
            locationLabel, _locationTextBox, _browseButton,
            _solutionCheckBox, _frameworkLabel, _frameworkCombo,
            _gitCheckBox, _standardLabel, _standardCombo,
            _statusLabel,
            _createButton, _cancelButton
        });

        AcceptButton = _createButton;
        CancelButton = _cancelButton;
    }

    private void TemplateList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_templateList.SelectedItems.Count == 0) return;
        var tpl = _templateList.SelectedItems[0].Tag as ProjectTemplate;
        bool isCsOrDotnet = tpl == null || tpl.Language == "C#";
        bool isCOrCpp     = tpl?.Language is "C" or "C++";
        bool isIaC        = tpl?.Language is "Bicep" or "Terraform";

        _solutionCheckBox.Visible = isCsOrDotnet;
        _frameworkLabel.Visible   = isCsOrDotnet;
        _frameworkCombo.Visible   = isCsOrDotnet;

        // C/C++ and IaC both show git init; C/C++ also shows language standard
        _gitCheckBox.Visible   = isCOrCpp || isIaC;
        _standardLabel.Visible = isCOrCpp;
        _standardCombo.Visible = isCOrCpp;

        if (isCOrCpp)
        {
            _standardCombo.Items.Clear();
            if (tpl!.Language == "C")
                _standardCombo.Items.AddRange(new object[] { "c17", "c11", "c99", "c89" });
            else
                _standardCombo.Items.AddRange(new object[] { "c++20", "c++17", "c++14", "c++11" });
            if (_standardCombo.Items.Count > 0) _standardCombo.SelectedIndex = 0;
        }
    }

    // ── Built-in templates ────────────────────────────────────────────────────

    private void BuildBuiltInTemplates()
    {
        // ── C templates ───────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Console App (C)", ShortName: "c-console", Language: "C",
            Tags: "Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.c", @"#include <stdio.h>

int main(int argc, char *argv[]) {
    printf(""Hello, World!\n"");
    return 0;
}
"),
                ("Makefile", @"CC      = gcc
CFLAGS  = -Wall -Wextra -g -std=$(STD)
STD     = c17
TARGET  = app
SRCS    = $(wildcard src/*.c)
OBJS    = $(SRCS:.c=.o)

.PHONY: all clean run

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CC) $(CFLAGS) -o $@ $^

%.o: %.c
	$(CC) $(CFLAGS) -c -o $@ $<

run: all
	./$(TARGET)

clean:
	rm -f $(OBJS) $(TARGET)
"),
                (".gitignore", "app\n*.o\n*.d\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Static Library (C)", ShortName: "c-staticlib", Language: "C",
            Tags: "Library", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.h", @"#ifndef {UNAME}_H
#define {UNAME}_H

int {name}_add(int a, int b);

#endif /* {UNAME}_H */
"),
                ("src/{name}.c", @"#include ""{name}.h""

int {name}_add(int a, int b) {
    return a + b;
}
"),
                ("examples/main.c", @"#include <stdio.h>
#include ""{name}.h""

int main(void) {
    printf(""%d\n"", {name}_add(2, 3));
    return 0;
}
"),
                ("Makefile", @"CC      = gcc
CFLAGS  = -Wall -Wextra -g -std=c17
AR      = ar
LIB     = lib{name}.a
SRCS    = $(wildcard src/*.c)
OBJS    = $(SRCS:.c=.o)

.PHONY: all clean

all: $(LIB)

$(LIB): $(OBJS)
	$(AR) rcs $@ $^

%.o: %.c
	$(CC) $(CFLAGS) -Iinclude -c -o $@ $<

clean:
	rm -f $(OBJS) $(LIB)
"),
                (".gitignore", "*.o\n*.a\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Makefile Project (C)", ShortName: "c-makefile", Language: "C",
            Tags: "Build", DotnetShortName: null,
            Files: new[]
            {
                ("main.c", @"#include <stdio.h>

int main(void) {
    printf(""Hello from {name}!\n"");
    return 0;
}
"),
                ("Makefile", @"CC     = gcc
CFLAGS = -Wall -Wextra -g
TARGET = {name}

all: $(TARGET)

$(TARGET): main.c
	$(CC) $(CFLAGS) -o $@ $<

clean:
	rm -f $(TARGET)
"),
                (".gitignore", "{name}\n*.o\n")
            }
        ));

        // ── C++ templates ─────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Console App (C++)", ShortName: "cpp-console", Language: "C++",
            Tags: "Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.cpp", @"#include <iostream>

int main(int argc, char* argv[]) {
    std::cout << ""Hello, World!\n"";
    return 0;
}
"),
                ("Makefile", @"CXX     = g++
CXXFLAGS = -Wall -Wextra -g -std=$(STD)
STD      = c++17
TARGET   = app
SRCS     = $(wildcard src/*.cpp)
OBJS     = $(SRCS:.cpp=.o)

.PHONY: all clean run

all: $(TARGET)

$(TARGET): $(OBJS)
	$(CXX) $(CXXFLAGS) -o $@ $^

%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c -o $@ $<

run: all
	./$(TARGET)

clean:
	rm -f $(OBJS) $(TARGET)
"),
                (".gitignore", "app\n*.o\nbuild/\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "CMake App (C++)", ShortName: "cpp-cmake", Language: "C++",
            Tags: "CMake/Console", DotnetShortName: null,
            Files: new[]
            {
                ("src/main.cpp", @"#include <iostream>

int main() {
    std::cout << ""Hello from {name}!\n"";
    return 0;
}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable({name} src/main.cpp)

target_compile_options({name} PRIVATE -Wall -Wextra)
"),
                (".gitignore", "build/\nCMakeFiles/\ncmake_install.cmake\nCMakeCache.txt\n"),
                ("README.md", @"# {name}

## Build

```bash
cmake -B build
cmake --build build
./build/{name}
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "CMake Library (C++)", ShortName: "cpp-cmakelib", Language: "C++",
            Tags: "CMake/Library", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.hpp", @"#pragma once

namespace {name} {{

int add(int a, int b);

}} // namespace {name}
"),
                ("src/{name}.cpp", @"#include ""{name}.hpp""

namespace {name} {{

int add(int a, int b) {{
    return a + b;
}}

}} // namespace {name}
"),
                ("examples/main.cpp", @"#include <iostream>
#include ""{name}.hpp""

int main() {{
    std::cout << {name}::add(2, 3) << '\n';
    return 0;
}}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library({name} src/{name}.cpp)
target_include_directories({name} PUBLIC include)

add_executable({name}_example examples/main.cpp)
target_link_libraries({name}_example PRIVATE {name})
"),
                (".gitignore", "build/\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Header-Only Library (C++)", ShortName: "cpp-header-only", Language: "C++",
            Tags: "Library/Header-Only", DotnetShortName: null,
            Files: new[]
            {
                ("include/{name}.hpp", @"#pragma once
#include <type_traits>

namespace {name} {{

template<typename T>
T clamp(T value, T lo, T hi) {{
    static_assert(std::is_arithmetic_v<T>, ""clamp requires arithmetic type"");
    return value < lo ? lo : value > hi ? hi : value;
}}

}} // namespace {name}
"),
                ("examples/main.cpp", @"#include <iostream>
#include ""{name}.hpp""

int main() {{
    std::cout << {name}::clamp(15, 0, 10) << '\n'; // 10
    return 0;
}}
"),
                ("CMakeLists.txt", @"cmake_minimum_required(VERSION 3.20)
project({name} VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library({name} INTERFACE)
target_include_directories({name} INTERFACE include)

add_executable({name}_example examples/main.cpp)
target_link_libraries({name}_example PRIVATE {name})
"),
                (".gitignore", "build/\n")
            }
        ));

        // ── Bicep templates ───────────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Resource Group Deployment", ShortName: "bicep-rg-deploy", Language: "Bicep",
            Tags: "Azure/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.bicep", @"@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Project name used as prefix for resource names')
param projectName string = '{name}'

// Storage account example
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${projectName}${environment}sa'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
  tags: {
    environment: environment
    project: projectName
    managedBy: 'bicep'
  }
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
"),
                ("parameters.dev.json", @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {
    ""environment"": { ""value"": ""dev"" },
    ""projectName"": { ""value"": ""{name}"" }
  }
}
"),
                ("parameters.prod.json", @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {
    ""environment"": { ""value"": ""prod"" },
    ""projectName"": { ""value"": ""{name}"" }
  }
}
"),
                (".gitignore", "*.json.local\n.azure/\n"),
                ("README.md", @"# {name}

Azure Bicep infrastructure deployment.

## Prerequisites
- Azure CLI: `winget install Microsoft.AzureCLI`
- Bicep CLI: `az bicep install`

## Deploy

```bash
# Login
az login

# Create resource group (first time)
az group create --name {name}-dev-rg --location eastus

# Build (compile to ARM)
az bicep build --file main.bicep

# Deploy
az deployment group create \
  --resource-group {name}-dev-rg \
  --template-file main.bicep \
  --parameters @parameters.dev.json

# Validate (lint)
az bicep lint --file main.bicep
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Bicep Module Library", ShortName: "bicep-modules", Language: "Bicep",
            Tags: "Azure/Modules/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.bicep", @"@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment name')
param environment string = 'dev'

module storage 'modules/storage.bicep' = {
  name: 'storageDeployment'
  params: {
    location: location
    environment: environment
  }
}

module network 'modules/network.bicep' = {
  name: 'networkDeployment'
  params: {
    location: location
    environment: environment
  }
}

output storageAccountName string = storage.outputs.storageAccountName
"),
                ("modules/storage.bicep", @"@description('Azure region')
param location string

@description('Environment')
param environment string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'st${environment}${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
  }
}

output storageAccountName string = storageAccount.name
"),
                ("modules/network.bicep", @"@description('Azure region')
param location string

@description('Environment')
param environment string

resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: 'vnet-${environment}'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: ['10.0.0.0/16']
    }
    subnets: [
      {
        name: 'default'
        properties: {
          addressPrefix: '10.0.0.0/24'
        }
      }
    ]
  }
}

output vnetId string = vnet.id
"),
                (".gitignore", "*.json.local\n.azure/\n"),
                ("README.md", "# {name}\n\nBicep module library.\n\n```bash\naz bicep build --file main.bicep\naz bicep lint --file main.bicep\n```\n")
            }
        ));

        // ── Terraform templates ───────────────────────────────────────────────────
        _allTemplates.Add(new ProjectTemplate(
            Name: "Azure Infrastructure (Terraform)", ShortName: "tf-azure", Language: "Terraform",
            Tags: "Azure/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"resource ""azurerm_resource_group"" ""main"" {
  name     = ""${var.project_name}-${var.environment}-rg""
  location = var.location

  tags = local.common_tags
}

resource ""azurerm_storage_account"" ""main"" {
  name                     = ""${var.project_name}${var.environment}sa""
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""
  min_tls_version          = ""TLS1_2""

  tags = local.common_tags
}
"),
                ("providers.tf", @"terraform {
  required_version = "">= 1.5""

  required_providers {
    azurerm = {
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }
  }
}

provider ""azurerm"" {
  features {}
}
"),
                ("variables.tf", @"variable ""project_name"" {
  type        = string
  description = ""Project name used as prefix for resource names""
  default     = ""{name}""
}

variable ""environment"" {
  type        = string
  description = ""Deployment environment (dev, staging, prod)""
  default     = ""dev""

  validation {
    condition     = contains([""dev"", ""staging"", ""prod""], var.environment)
    error_message = ""Environment must be dev, staging, or prod.""
  }
}

variable ""location"" {
  type        = string
  description = ""Azure region""
  default     = ""eastus""
}
"),
                ("outputs.tf", @"output ""resource_group_name"" {
  description = ""Name of the created resource group""
  value       = azurerm_resource_group.main.name
}

output ""storage_account_name"" {
  description = ""Name of the storage account""
  value       = azurerm_storage_account.main.name
}
"),
                ("locals.tf", @"locals {
  common_tags = {
    environment = var.environment
    project     = var.project_name
    managed_by  = ""terraform""
  }
}
"),
                (".gitignore", "# Terraform state and secrets\n.terraform/\n*.tfstate\n*.tfstate.backup\n*.tfstate.*.backup\n.terraform.lock.hcl\nterraform.tfvars\n*.auto.tfvars\ncrash.log\n"),
                ("terraform.tfvars.example", "project_name = \"{name}\"\nenvironment  = \"dev\"\nlocation     = \"eastus\"\n"),
                ("README.md", @"# {name}

Azure infrastructure managed with Terraform.

## Prerequisites
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5
- [Azure CLI](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)

## Usage

```bash
# Login
az login

# Copy and edit variables
cp terraform.tfvars.example terraform.tfvars

# Initialize
terraform init

# Plan
terraform plan

# Apply
terraform apply

# Destroy
terraform destroy
```
")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "AWS Infrastructure (Terraform)", ShortName: "tf-aws", Language: "Terraform",
            Tags: "AWS/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"resource ""aws_s3_bucket"" ""main"" {
  bucket = ""${var.project_name}-${var.environment}-${random_id.suffix.hex}""

  tags = local.common_tags
}

resource ""aws_s3_bucket_versioning"" ""main"" {
  bucket = aws_s3_bucket.main.id
  versioning_configuration {
    status = ""Enabled""
  }
}

resource ""random_id"" ""suffix"" {
  byte_length = 4
}
"),
                ("providers.tf", @"terraform {
  required_version = "">= 1.5""

  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
    random = {
      source  = ""hashicorp/random""
      version = ""~> 3.0""
    }
  }
}

provider ""aws"" {
  region = var.aws_region
}
"),
                ("variables.tf", @"variable ""project_name"" {
  type        = string
  description = ""Project name""
  default     = ""{name}""
}

variable ""environment"" {
  type        = string
  description = ""Deployment environment""
  default     = ""dev""
}

variable ""aws_region"" {
  type        = string
  description = ""AWS region""
  default     = ""us-east-1""
}
"),
                ("outputs.tf", @"output ""bucket_name"" {
  description = ""S3 bucket name""
  value       = aws_s3_bucket.main.bucket
}

output ""bucket_arn"" {
  description = ""S3 bucket ARN""
  value       = aws_s3_bucket.main.arn
}
"),
                ("locals.tf", @"locals {
  common_tags = {
    Environment = var.environment
    Project     = var.project_name
    ManagedBy   = ""terraform""
  }
}
"),
                (".gitignore", "# Terraform\n.terraform/\n*.tfstate\n*.tfstate.backup\n.terraform.lock.hcl\nterraform.tfvars\n*.auto.tfvars\ncrash.log\n"),
                ("terraform.tfvars.example", "project_name = \"{name}\"\nenvironment  = \"dev\"\naws_region   = \"us-east-1\"\n"),
                ("README.md", "# {name}\n\nAWS infrastructure managed with Terraform.\n\n```bash\nterraform init\nterraform plan\nterraform apply\n```\n")
            }
        ));

        _allTemplates.Add(new ProjectTemplate(
            Name: "Reusable Module (Terraform)", ShortName: "tf-module", Language: "Terraform",
            Tags: "Module/IaC", DotnetShortName: null,
            Files: new[]
            {
                ("main.tf", @"# Module: {name}
# Place your resource definitions here.
# Example:
# resource ""azurerm_resource_group"" ""this"" {
#   name     = var.resource_group_name
#   location = var.location
# }
"),
                ("variables.tf", @"variable ""name"" {
  type        = string
  description = ""Resource name""
}

variable ""location"" {
  type        = string
  description = ""Azure region / AWS region""
  default     = ""eastus""
}

variable ""tags"" {
  type        = map(string)
  description = ""Tags to apply to all resources""
  default     = {}
}
"),
                ("outputs.tf", @"# output ""resource_id"" {
#   description = ""ID of the created resource""
#   value       = azurerm_xxx.this.id
# }
"),
                ("versions.tf", @"terraform {
  required_version = "">= 1.5""
  required_providers {
    # Add your providers here
  }
}
"),
                ("examples/main.tf", @"module ""{name}"" {
  source = ""..""

  name     = ""example""
  location = ""eastus""
  tags = {
    environment = ""dev""
    managedBy   = ""terraform""
  }
}
"),
                ("README.md", @"# {name} Terraform Module

## Usage

```hcl
module ""{name}"" {{
  source = ""path/to/{name}""

  name     = ""my-resource""
  location = ""eastus""
}}
```

## Inputs

| Name | Type | Default | Description |
|------|------|---------|-------------|
| name | string | — | Resource name |
| location | string | eastus | Region |
| tags | map(string) | {} | Resource tags |

## Outputs

| Name | Description |
|------|-------------|
"),
                (".gitignore", ".terraform/\n*.tfstate\n*.tfstate.backup\n.terraform.lock.hcl\n")
            }
        ));
    }

    // ── Template list population ──────────────────────────────────────────────

    private void PopulateTemplateList(string langFilter)
    {
        _templateList.Items.Clear();
        var filtered = langFilter == "All"
            ? _allTemplates
            : _allTemplates.Where(t => t.Language == langFilter).ToList();

        foreach (var tpl in filtered)
        {
            var item = new ListViewItem(tpl.ShortName);
            item.SubItems.Add(tpl.Name);
            item.SubItems.Add(tpl.Language);
            item.SubItems.Add(tpl.Tags);
            item.Tag = tpl;
            _templateList.Items.Add(item);
        }

        if (_templateList.Items.Count > 0)
            _templateList.Items[0].Selected = true;
    }

    private void LoadDotnetTemplates()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "new list")
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(20000);
            if (proc.ExitCode != 0) return;
            ParseDotnetTemplateList(stdout);
        }
        catch { /* fallback: built-in .NET templates already in list */ }

        // Ensure at least common C# fallbacks
        if (!_allTemplates.Any(t => t.Language == "C#"))
        {
            foreach (var (shortName, name, tags) in new[]
            {
                ("console",  "Console App",          "Common/Console"),
                ("classlib", "Class Library",         "Common/Library"),
                ("webapi",   "ASP.NET Core Web API",  "Web/API"),
                ("mstest",   "MSTest Test Project",   "Test/MSTest"),
            })
            {
                _allTemplates.Add(new ProjectTemplate(name, shortName, "C#", tags, shortName,
                    Array.Empty<(string, string)>()));
            }
        }
    }

    private void ParseDotnetTemplateList(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool inData = false;
        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.StartsWith("---")) { inData = true; continue; }
            if (!inData || string.IsNullOrEmpty(line)) continue;

            var parts = Regex.Split(line, @"\s{2,}");
            if (parts.Length < 2) continue;

            string tplName  = parts[0].Trim();
            string shortName = parts.Length > 1 ? parts[1].Trim() : "";
            string language  = parts.Length > 2 ? parts[2].Trim().Trim('[', ']') : "C#";
            string tags      = parts.Length > 3 ? parts[3].Trim() : "";

            if (!language.Equals("C#", StringComparison.OrdinalIgnoreCase)) continue;

            _allTemplates.Add(new ProjectTemplate(tplName, shortName, "C#", tags, shortName,
                Array.Empty<(string, string)>()));
        }
    }

    private void LoadDotnetFrameworks()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-sdks")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            var versions = new SortedSet<string>(Comparer<string>.Create((a, b) => string.Compare(b, a, StringComparison.OrdinalIgnoreCase)));
            foreach (Match m in Regex.Matches(stdout, @"(\d+\.\d+)\.\d+"))
                versions.Add($"net{m.Groups[1].Value}");

            foreach (var v in versions) _frameworkCombo.Items.Add(v);
        }
        catch { }

        if (_frameworkCombo.Items.Count == 0)
        {
            _frameworkCombo.Items.Add("net9.0");
            _frameworkCombo.Items.Add("net8.0");
        }
        if (_frameworkCombo.Items.Count > 0) _frameworkCombo.SelectedIndex = 0;
    }

    private void ApplyTheme()
    {
        var theme = ThemeManager.Instance.CurrentTheme;
        BackColor = theme.Background;
        ForeColor = theme.Text;

        foreach (Control c in Controls)
        {
            if (c is TextBox tb) { tb.BackColor = theme.EditorBackground; tb.ForeColor = theme.Text; }
            else if (c is ComboBox cb) { cb.BackColor = theme.EditorBackground; cb.ForeColor = theme.Text; }
            else if (c is Button btn) { btn.BackColor = theme.PanelBackground; btn.ForeColor = theme.Text; }
            else if (c is CheckBox chk) { chk.ForeColor = theme.Text; }
            else if (c is Label lbl) { lbl.ForeColor = theme.Text; }
        }
        _templateList.BackColor = theme.EditorBackground;
        _templateList.ForeColor = theme.Text;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = _locationTextBox.Text };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _locationTextBox.Text = dlg.SelectedPath;
    }

    // ── Project creation ──────────────────────────────────────────────────────

    private async void CreateButton_Click(object? sender, EventArgs e)
    {
        if (_creating) return;

        var tpl = _templateList.SelectedItems.Count > 0
            ? _templateList.SelectedItems[0].Tag as ProjectTemplate : null;
        string projectName = _nameTextBox.Text.Trim();
        string location    = _locationTextBox.Text.Trim();

        if (tpl == null)       { _statusLabel.Text = "Please select a template."; return; }
        if (string.IsNullOrEmpty(projectName)) { _statusLabel.Text = "Please enter a project name."; return; }
        if (!Directory.Exists(location))       { _statusLabel.Text = "Please select a valid location."; return; }

        _creating = true;
        _createButton.Enabled = false;
        _cancelButton.Enabled = false;
        _statusLabel.ForeColor = Color.DimGray;

        string projectDir = Path.Combine(location, projectName);

        try
        {
            if (tpl.DotnetShortName != null)
                await CreateDotnetProjectAsync(tpl, projectName, projectDir, location);
            else
                await CreateNativeProjectAsync(tpl, projectName, projectDir);

            _statusLabel.Text = "Project created successfully!";
            _statusLabel.ForeColor = Color.DarkGreen;

            _mainForm.BeginInvoke(() => _mainForm.OpenWorkspaceFolder(projectDir));
            await Task.Delay(500);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _statusLabel.ForeColor = Color.DarkRed;
            _statusLabel.Text = $"Error: {ex.Message}";
            _creating = false;
            _createButton.Enabled = true;
            _cancelButton.Enabled = true;
        }
    }

    private async Task CreateDotnetProjectAsync(ProjectTemplate tpl, string projectName, string projectDir, string location)
    {
        _statusLabel.Text = $"Creating {tpl.Language} project '{projectName}'...";
        string? framework = _frameworkCombo.SelectedItem as string;

        string newArgs = $"new \"{tpl.DotnetShortName}\" -n \"{projectName}\" -o \"{projectDir}\"";
        if (!string.IsNullOrEmpty(framework))
            newArgs += $" --framework {framework}";

        await RunProcessAsync("dotnet", newArgs);

        if (_solutionCheckBox.Checked)
        {
            _statusLabel.Text = "Creating solution file...";
            await RunProcessAsync("dotnet", $"new sln -o \"{location}\"");
            string? csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj != null)
            {
                _statusLabel.Text = "Adding project to solution...";
                await RunProcessAsync("dotnet", $"sln \"{location}\" add \"{csproj}\"");
            }
        }
    }

    private async Task CreateNativeProjectAsync(ProjectTemplate tpl, string projectName, string projectDir)
    {
        _statusLabel.Text = $"Creating {tpl.Language} project '{projectName}'...";

        Directory.CreateDirectory(projectDir);

        string uname = projectName.ToUpperInvariant();
        // _standardCombo is only visible/relevant for C/C++ — IaC templates don't use {std}
        string std = _standardCombo.Visible
            ? (_standardCombo.SelectedItem as string ?? (tpl.Language == "C" ? "c17" : "c++17"))
            : "";

        foreach (var (relPath, rawContent) in tpl.Files)
        {
            // Expand template variables
            string content = rawContent
                .Replace("{name}", projectName)
                .Replace("{UNAME}", uname)
                .Replace("{std}", std);

            // Expand relPath too (e.g. include/{name}.h)
            string expandedPath = relPath
                .Replace("{name}", projectName)
                .Replace("{UNAME}", uname);

            string fullPath = Path.Combine(projectDir, expandedPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content);
        }

        if (_gitCheckBox.Checked)
        {
            _statusLabel.Text = "Initializing git repository...";
            try { await RunProcessAsync("git", "init", projectDir); } catch { /* git not on PATH, skip */ }
        }
    }

    private static async Task<string> RunProcessAsync(string exe, string args, string? workDir = null)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true,
        };
        if (workDir != null) psi.WorkingDirectory = workDir;

        using var proc = Process.Start(psi) ?? throw new Exception($"Failed to start {exe}");
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new Exception(string.IsNullOrWhiteSpace(stderr) ? $"{exe} exited with code {proc.ExitCode}" : stderr.Trim());

        return stdout;
    }
}
