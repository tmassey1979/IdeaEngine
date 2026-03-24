export interface GeneratedArtifact {
  path: string;
  content: string;
}

export function generateProjectSlice(name: string): GeneratedArtifact[] {
  return [
    {
      path: `src/${name}.ts`,
      content: `export const ${name} = true;`
    }
  ];
}