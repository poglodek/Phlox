export interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  createdAt: string;
}

export interface Chat {
  id: string;
  title: string | null;
  createdAt: string;
  updatedAt: string | null;
  messages: Message[];
}

export interface CreateChatRequest {
  title?: string;
}

export interface SendMessageRequest {
  content: string;
}

export interface StreamChunk {
  content?: string;
  error?: string;
}
