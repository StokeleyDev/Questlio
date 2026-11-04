param([string]$NodeModules)
$ErrorActionPreference='Stop'
$oldNodePath=$env:NODE_PATH
try {
    if(-not $NodeModules){
        $tools=Join-Path $PSScriptRoot '.glass-tools'
        & npm install --prefix $tools --no-save --package-lock=false 'react@19.3.1' 'react-dom@19.3.1' '@zakisheriff/liquid-glass@0.3.16' 'esbuild@0.25.12'
        if($LASTEXITCODE -ne 0){throw 'Liquid Glass dependency install failed'}
        $NodeModules=Join-Path $tools 'node_modules'
    }
    $env:NODE_PATH=(Resolve-Path -LiteralPath $NodeModules).Path
    & node (Join-Path $env:NODE_PATH 'esbuild/bin/esbuild') (Join-Path $PSScriptRoot 'glass-ui/controls.jsx') --bundle --minify --format=iife --platform=browser --target=chrome120 ('--outfile='+(Join-Path $PSScriptRoot 'ui/react-glass.js')) --legal-comments=eof
    if($LASTEXITCODE -ne 0){throw 'Liquid Glass bundle build failed'}
    foreach($package in @('react','react-dom')){
        Copy-Item -LiteralPath (Join-Path $env:NODE_PATH ($package+'/LICENSE')) -Destination (Join-Path $PSScriptRoot ('ui/'+$package+'-LICENSE.txt'))
    }
    Copy-Item -LiteralPath (Join-Path $env:NODE_PATH '@zakisheriff/liquid-glass/LICENSE') -Destination (Join-Path $PSScriptRoot 'ui/liquid-glass-LICENSE.txt')
} finally { $env:NODE_PATH=$oldNodePath }
