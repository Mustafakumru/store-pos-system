export interface AttributeInput {
  name:   string;
  values: string[];
}

export interface VariantCombination {
  attributes: Record<string, string>;
  color:      string;
  size:       string;
  barcode:    string;
  price:      number | null;
  selected:   boolean;
}

const COLOR_NAMES = ['renk', 'color', 'colour'];
const SIZE_NAMES  = ['beden', 'size', 'numara', 'ölçü'];

export function generateVariantMatrix(
  attributes: AttributeInput[]
): VariantCombination[] {
  if (!attributes.length || attributes.some(a => !a.values.length))
    return [];

  function cartesian(
    arrays:  string[][],
    current: string[] = [],
    index    = 0,
    result:  string[][] = []
  ): string[][] {
    if (index === arrays.length) {
      result.push([...current]);
      return result;
    }
    for (const value of arrays[index])
      cartesian(arrays, [...current, value], index + 1, result);
    return result;
  }

  const combinations = cartesian(attributes.map(a => a.values));

  // Renk/beden SIRAYA göre değil ÖZELLİK ADINA göre belirlenir (eskiden ilk
  // özellik renk, ikincisi beden sayılıyordu - "Renk" silinip sadece "Beden"
  // bırakılınca bedenler renk olarak kaydediliyordu). Renk dışındaki tüm
  // özellikler (Beden + varsa ek özellikler) beden alanında birleştirilir;
  // böylece ek özellik farklı kombinasyonlar aynı varyanta çakışmaz.
  const norm = (n: string) => n.trim().toLocaleLowerCase('tr');
  const colorIdx = attributes.findIndex(a => COLOR_NAMES.includes(norm(a.name)));
  const sizeIdx  = attributes.findIndex(a => SIZE_NAMES.includes(norm(a.name)));
  const otherIdx = attributes
    .map((_, i) => i)
    .filter(i => i !== colorIdx && i !== sizeIdx);
  const sizeParts = (sizeIdx >= 0 ? [sizeIdx] : []).concat(otherIdx);

  return combinations.map(combo => {
    const attrMap: Record<string, string> = {};
    attributes.forEach((attr, i) => attrMap[attr.name] = combo[i]);

    return {
      attributes: attrMap,
      color:      colorIdx >= 0 ? combo[colorIdx] : '',
      size:       sizeParts.map(i => combo[i]).join(' / '),
      barcode:    '',
      price:      null,
      selected:   true
    };
  });
}

export function groupByColor(
  variants: VariantCombination[]
): Map<string, VariantCombination[]> {
  const map = new Map<string, VariantCombination[]>();
  for (const v of variants) {
    if (!map.has(v.color)) map.set(v.color, []);
    map.get(v.color)!.push(v);
  }
  return map;
}

export function applyBulkPrice(
  variants: VariantCombination[],
  price:    number
): VariantCombination[] {
  return variants.map(v => v.selected ? { ...v, price } : v);
}

export function generateBarcodes(
  variants: VariantCombination[],
  prefix    = 'LCW'
): VariantCombination[] {
  let counter = 1;
  return variants.map(v => ({
    ...v,
    barcode: v.selected
      ? `${prefix}${String(counter++).padStart(4, '0')}`
      : v.barcode
  }));
}