namespace Domain.Enums;

/// <summary>Urun sezonu. Kodun 1. karakteri: W = Kis, S = Yaz.</summary>
public enum Season
{
    Winter = 1,   // W
    Summer = 2    // S
}

/// <summary>
/// Urunun hitap ettigi bolum (reyon). Yas kitlesini (Z kodu) belirler:
/// Kadin / Erkek -> Z8 (yetiskin), Kiz / Erkek Cocuk -> Z4 (10-14 yas), Bebek -> Z1.
/// </summary>
public enum Department
{
    Women = 1,
    Men = 2,
    Girl = 3,
    Boy = 4,
    Baby = 5
}

/// <summary>Yas kitlesi. Kodun son iki karakteri: Z1 / Z4 / Z8 (baska Z kodu yoktur).</summary>
public enum AgeGroup
{
    Baby = 1,    // Z1
    Kids = 4,    // Z4 (10-14 yas)
    Adult = 8    // Z8
}
