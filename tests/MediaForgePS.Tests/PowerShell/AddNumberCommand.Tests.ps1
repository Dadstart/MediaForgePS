BeforeAll {
    Import-Module "$PSScriptRoot\..\..\src\MediaForgePS\MediaForgePS.psd1" -Force
}

Describe 'Add-Number' {
    It 'Should add two positive numbers correctly' {
        $result = Add-Number -FirstNumber 5 -SecondNumber 3
        $result | Should -Be 8
    }

    It 'Should add negative numbers correctly' {
        $result = Add-Number -FirstNumber -5 -SecondNumber -3
        $result | Should -Be -8
    }

    It 'Should add zero correctly' {
        $result = Add-Number -FirstNumber 10 -SecondNumber 0
        $result | Should -Be 10
    }

    It 'Should add decimal numbers correctly' {
        $result = Add-Number -FirstNumber 5.5 -SecondNumber 3.7
        $result | Should -Be 9.2
    }

    It 'Should accept pipeline input for FirstNumber' {
        $result = 5 | Add-Number -SecondNumber 3
        $result | Should -Be 8
    }
}
