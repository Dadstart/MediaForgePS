BeforeAll {
    Import-Module "$PSScriptRoot\..\..\src\MediaForgePS\MediaForgePS.psd1" -Force
}

Describe 'Subtract-Number' {
    It 'Should subtract two numbers correctly' {
        $result = Subtract-Number -Minuend 10 -Subtrahend 3
        $result | Should -Be 7
    }

    It 'Should handle negative results correctly' {
        $result = Subtract-Number -Minuend 5 -Subtrahend 10
        $result | Should -Be -5
    }

    It 'Should subtract zero correctly' {
        $result = Subtract-Number -Minuend 10 -Subtrahend 0
        $result | Should -Be 10
    }

    It 'Should subtract decimal numbers correctly' {
        $result = Subtract-Number -Minuend 10.5 -Subtrahend 3.7
        $result | Should -Be 6.8
    }

    It 'Should accept pipeline input for Minuend' {
        $result = 10 | Subtract-Number -Subtrahend 3
        $result | Should -Be 7
    }
}
