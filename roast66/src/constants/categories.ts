const CategoryType = {
  COFFEE: 0,
  SPECIALS: 1,
  FLAVORS: 2,
  DRINKS: 3,
} as const;

export type CategoryTypeValue = (typeof CategoryType)[keyof typeof CategoryType];

export default CategoryType;
