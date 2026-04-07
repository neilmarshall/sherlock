export interface Story {
  id: string;
  title: string;
  collection: string;
  yearPublished: number;
  wordCount: number;
  body: string;
}

export type StoryMetadata = Omit<Story, 'body'>;
